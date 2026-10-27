/**
 * Idle Sign-out (#332): signs the Account out after the configured minutes with no user activity in any open tab.
 * Activity is keyboard, mouse, scroll or touch input; the time of the last one is shared across tabs through
 * localStorage, so every tab counts down together. While the user is active the script checks in with the server, at
 * most once every 5 minutes and only after input since the last check-in, which renews the sign-in cookie (the
 * server's limit). At timeout minus the warning a dialog opens in every tab; on expiry the script signs out through the
 * normal sign-out and every tab goes to Login with the inactivity message and its own return address.
 * The limits and URLs come from the layout's #idleSignOutDialog, which renders only for signed-in users.
 */
(function () {
    'use strict';

    const CHECK_IN_INTERVAL_MS = 5 * 60 * 1000;
    const ACTIVITY_WRITE_INTERVAL_MS = 1000;
    const TICK_MS = 1000;
    const ANNOUNCE_EVERY_SECONDS = 15;
    const ANNOUNCE_FINAL_SECONDS = 10;
    const KEYS = {
        lastActivity: 'pf.idleSignOut.lastActivity',
        lastCheckIn: 'pf.idleSignOut.lastCheckIn',
        signedOut: 'pf.idleSignOut.signedOut'
    };
    const ACTIVITY_EVENTS = ['keydown', 'mousedown', 'mousemove', 'wheel', 'scroll', 'touchstart'];

    let config = null;
    let dialog = null;
    let countdown = null;
    let announcement = null;
    let lastActivityWrite = 0;
    let signingOut = false;
    let leaving = false;
    let tickTimer = null;
    // Used when localStorage is unavailable (private mode, blocked storage): the tab counts on its own.
    const memory = {};

    /**
     * Read a shared timestamp.
     * @param {string} key - One of KEYS.
     * @returns {number} Milliseconds since the epoch, or 0 when unset.
     */
    function readTime (key) {
        let raw = null;
        try {
            raw = window.localStorage.getItem(key);
        } catch (error) {
            raw = memory[key] || null;
        }
        const value = Number(raw);
        return Number.isFinite(value) ? value : 0;
    }

    /**
     * Write a shared value; other tabs receive it as a storage event.
     * @param {string} key - One of KEYS.
     * @param {string|number} value - The value to store.
     * @returns {void}
     */
    function write (key, value) {
        memory[key] = String(value);
        try {
            window.localStorage.setItem(key, String(value));
        } catch (error) {
            // Storage blocked: this tab still works from memory.
        }
    }

    /**
     * Seconds left before the sign-out, from the last activity in any tab.
     * @returns {number} Whole seconds, never negative.
     */
    function secondsLeft () {
        const idleMs = Date.now() - readTime(KEYS.lastActivity);
        return Math.max(0, Math.ceil((config.timeoutSeconds * 1000 - idleMs) / 1000));
    }

    /**
     * True while the warning is due, when only the dialog's buttons keep the Account signed in.
     * @returns {boolean} Whether the warning period has started.
     */
    function inWarning () {
        return secondsLeft() <= config.warningSeconds;
    }

    /**
     * Format seconds as m:ss.
     * @param {number} seconds - Whole seconds.
     * @returns {string} The countdown text.
     */
    function formatTime (seconds) {
        const minutes = Math.floor(seconds / 60);
        const rest = seconds % 60;
        return `${minutes}:${rest < 10 ? '0' : ''}${rest}`;
    }

    /**
     * The Login URL with the inactivity message (when idle) and this tab's address to return to.
     * @param {boolean} idle - Whether the sign-out was for inactivity.
     * @returns {string} The URL to navigate to.
     */
    function loginUrl (idle) {
        const returnUrl = window.location.pathname + window.location.search + window.location.hash;
        const params = new URLSearchParams({ ReturnUrl: returnUrl });
        if (idle) {
            params.set('idle', 'true');
        }
        return `${config.loginUrl}?${params.toString()}`;
    }

    /**
     * Leave for Login, once.
     * @param {boolean} idle - Whether to show the inactivity message.
     * @returns {void}
     */
    function goToLogin (idle) {
        if (leaving) {
            return;
        }
        leaving = true;
        window.clearInterval(tickTimer);
        window.location.assign(loginUrl(idle));
    }

    /**
     * Sign out through the normal sign-out, tell the other tabs, then go to Login.
     * @param {boolean} idle - Whether the sign-out was for inactivity.
     * @returns {Promise<void>}
     */
    async function signOut (idle) {
        if (signingOut || leaving) {
            return;
        }
        signingOut = true;
        window.clearInterval(tickTimer);
        try {
            await postWithToken(config.logoutUrl);
        } catch (error) {
            // Offline or already signed out: the cookie's own expiry still ends the session.
        }
        announceSignedOut(idle);
        goToLogin(idle);
    }

    /**
     * Tell the other tabs this tab is signed out, so they go to Login too.
     * @param {boolean} idle - Whether the sign-out was for inactivity.
     * @returns {void}
     */
    function announceSignedOut (idle) {
        write(KEYS.signedOut, JSON.stringify({ idle: idle, at: Date.now() }));
    }

    /**
     * POST with the antiforgery token, marked as a script request so a signed-out answer is 401, not a redirect.
     * @param {string} url - The URL to post to.
     * @returns {Promise<Response>} The response.
     */
    function postWithToken (url) {
        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'RequestVerificationToken': window.PFUtils.getAntiForgeryToken(),
                'X-Requested-With': 'XMLHttpRequest'
            }
        });
    }

    /**
     * Tell the server the user is still here; this renews the sign-in cookie. The attempt is recorded before it is
     * sent, so no other tab sends one at the same time and a failure waits for the next 5-minute slot. A 401 means the
     * server already signed the Account out.
     * @returns {Promise<void>}
     */
    async function checkIn () {
        if (leaving) {
            return;
        }
        write(KEYS.lastCheckIn, Date.now());
        try {
            const response = await postWithToken(config.checkInUrl);
            if (response.status === 401) {
                announceSignedOut(true);
                goToLogin(true);
            }
        } catch (error) {
            // Network failure: the next check-in after new input tries again.
        }
    }

    /**
     * Check in when there was input since the last check-in and 5 minutes have passed since it.
     * @returns {void}
     */
    function checkInIfDue () {
        const lastCheckIn = readTime(KEYS.lastCheckIn);
        if (readTime(KEYS.lastActivity) > lastCheckIn && Date.now() - lastCheckIn >= CHECK_IN_INTERVAL_MS) {
            checkIn();
        }
    }

    /**
     * Record user input, at most once a second; input during the warning does not count.
     * @returns {void}
     */
    function onActivity () {
        const now = Date.now();
        if (leaving || now - lastActivityWrite < ACTIVITY_WRITE_INTERVAL_MS || inWarning()) {
            return;
        }
        lastActivityWrite = now;
        write(KEYS.lastActivity, now);
        checkInIfDue();
    }

    /**
     * Update the visible countdown, and the screen-reader announcement every 15 seconds and in the last 10.
     * @param {number} seconds - Seconds left.
     * @param {boolean} opening - Whether the dialog has just opened.
     * @returns {void}
     */
    function renderCountdown (seconds, opening) {
        const text = window.PFI18n.t('Shell.Js.IdleSignOut.Countdown', { time: formatTime(seconds) });
        if (countdown) {
            countdown.textContent = text;
        }
        if (announcement && (opening || seconds % ANNOUNCE_EVERY_SECONDS === 0 || seconds <= ANNOUNCE_FINAL_SECONDS)) {
            announcement.textContent = text;
        }
    }

    /**
     * Open the warning dialog, which takes keyboard focus.
     * @param {number} seconds - Seconds left.
     * @returns {void}
     */
    function openWarning (seconds) {
        if (dialog.open) {
            renderCountdown(seconds, false);
            return;
        }
        renderCountdown(seconds, true);
        if (typeof dialog.showModal === 'function') {
            dialog.showModal();
        } else {
            dialog.setAttribute('open', '');
        }
        const stay = document.getElementById('idleSignOutStay');
        if (stay) {
            stay.focus();
        }
    }

    /**
     * Close the warning dialog, as when another tab chose to stay signed in.
     * @returns {void}
     */
    function closeWarning () {
        if (!dialog.open) {
            return;
        }
        if (typeof dialog.close === 'function') {
            dialog.close();
        } else {
            dialog.removeAttribute('open');
        }
        if (announcement) {
            announcement.textContent = '';
        }
    }

    /**
     * Once a second: sign out on expiry, show or hide the warning, and send a check-in that input made due.
     * @returns {void}
     */
    function tick () {
        if (leaving) {
            return;
        }
        const seconds = secondsLeft();
        if (seconds <= 0) {
            signOut(true);
            return;
        }
        if (seconds <= config.warningSeconds) {
            openWarning(seconds);
        } else {
            closeWarning();
            checkInIfDue();
        }
    }

    /**
     * "Stay signed in": counts as activity in every tab and checks in straight away.
     * @returns {void}
     */
    function staySignedIn () {
        const now = Date.now();
        lastActivityWrite = now;
        write(KEYS.lastActivity, now);
        closeWarning();
        checkIn();
    }

    /**
     * Follow the other tabs: a sign-out sends this tab to Login, a new activity time closes the warning.
     * @param {StorageEvent} event - The storage event.
     * @returns {void}
     */
    function onStorage (event) {
        if (event.key === KEYS.signedOut && event.newValue) {
            let idle = true;
            try {
                idle = JSON.parse(event.newValue).idle !== false;
            } catch (error) {
                idle = true;
            }
            goToLogin(idle);
        } else if (event.key === KEYS.lastActivity) {
            tick();
        }
    }

    /**
     * The normal Sign out button signs out every tab, without the inactivity message.
     * @param {SubmitEvent} event - A form submission anywhere on the page.
     * @returns {void}
     */
    function onSubmit (event) {
        const form = event.target;
        const action = form && form.getAttribute ? form.getAttribute('action') || '' : '';
        if (action.split('?')[0].toLowerCase() === config.logoutUrl.toLowerCase()) {
            announceSignedOut(false);
        }
    }

    /**
     * Read the limits from the layout, start counting from this page load, and wire the events.
     * @returns {void}
     */
    function init () {
        dialog = document.getElementById('idleSignOutDialog');
        if (!dialog || !window.PFUtils || !window.PFI18n) {
            return;
        }
        config = {
            timeoutSeconds: Number(dialog.dataset.timeoutSeconds),
            warningSeconds: Number(dialog.dataset.warningSeconds),
            checkInUrl: dialog.dataset.checkInUrl || '',
            logoutUrl: dialog.dataset.logoutUrl || '',
            loginUrl: dialog.dataset.loginUrl || ''
        };
        if (!(config.timeoutSeconds > 0) || !(config.warningSeconds > 0) || !config.checkInUrl || !config.logoutUrl) {
            return;
        }
        countdown = document.getElementById('idleSignOutCountdown');
        announcement = document.getElementById('idleSignOutAnnouncement');

        // The server just accepted this page, so a stale shared time from an earlier session cannot sign it out.
        if (secondsLeft() <= config.warningSeconds) {
            write(KEYS.lastActivity, Date.now());
        }

        ACTIVITY_EVENTS.forEach((name) => {
            document.addEventListener(name, onActivity, { capture: true, passive: true });
        });
        document.addEventListener('submit', onSubmit, true);
        window.addEventListener('storage', onStorage);

        const stay = document.getElementById('idleSignOutStay');
        if (stay) {
            stay.addEventListener('click', staySignedIn);
        }
        const signOutNow = document.getElementById('idleSignOutSignOut');
        if (signOutNow) {
            signOutNow.addEventListener('click', () => signOut(false));
        }
        // Escape closes a modal dialog; treat it as "Stay signed in" so the countdown and the dialog agree.
        dialog.addEventListener('cancel', (event) => {
            event.preventDefault();
            staySignedIn();
        });

        tickTimer = window.setInterval(tick, TICK_MS);
        tick();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
