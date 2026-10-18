(function () {
    'use strict';

    /**
     * Cookie consent manager — shows banner, stores choice, and controls
     * third-party script loading (Google Fonts, unpkg Lucide).
     */
    const cookieName = 'pf_cookie_consent';
    const expiryDays = 180;

    /**
     * Check whether the user has already given consent.
     * @returns {string|null} 'all', 'essential', or null if no consent yet.
     */
    function getConsent () {
        const match = document.cookie.match(new RegExp('(^| )' + cookieName + '=([^;]+)'));
        if (!match) { return null; }
        return decodeURIComponent(match[2]);
    }

    /**
     * Store the user's consent choice in a cookie.
     * @param {string} choice - 'all' or 'essential'.
     */
    function setConsent (choice) {
        const expires = new Date();
        expires.setDate(expires.getDate() + expiryDays);
        document.cookie = cookieName + '=' + encodeURIComponent(choice) +
            ';expires=' + expires.toUTCString() + ';path=/;SameSite=Lax';
    }

    /**
     * Load Google Fonts and unpkg Lucide stylesheets after consent.
     */
    function loadThirdParty () {
        // Google Fonts
        if (!document.querySelector('link[href*="fonts.googleapis.com"]')) {
            const fontLink = document.createElement('link');
            fontLink.rel = 'preconnect';
            fontLink.href = 'https://fonts.googleapis.com';
            document.head.appendChild(fontLink);

            const fontJsStaticLink = document.createElement('link');
            fontJsStaticLink.rel = 'preconnect';
            fontJsStaticLink.href = 'https://fonts.gstatic.com';
            fontJsStaticLink.crossOrigin = 'anonymous';
            document.head.appendChild(fontJsStaticLink);

            const fontCss = document.createElement('link');
            fontCss.href = 'https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;500&family=Inter:wght@400;500;600;700&display=swap';
            fontCss.rel = 'stylesheet';
            document.head.appendChild(fontCss);
        }

        // Lucide icons (pinned version)
        if (!document.querySelector('script[src*="unpkg.com/lucide"]')) {
            const script = document.createElement('script');
            script.src = 'https://unpkg.com/lucide@0.378.0';
            script.onload = function () {
                if (typeof lucide !== 'undefined') {
                    lucide.createIcons();
                }
            };
            document.head.appendChild(script);
        }
    }

    /**
     * Hide the cookie banner.
     */
    function hideBanner () {
        const banner = document.getElementById('cookieBanner');
        if (banner) {
            banner.style.display = 'none';
        }
    }

    /**
     * Initialize the cookie consent module.
     */
    function init () {
        const consent = getConsent();

        // If already consented, load third-party resources silently
        if (consent === 'all') {
            loadThirdParty();
            return;
        }

        // If essential only, keep banner hidden and don't load third-party
        if (consent === 'essential') {
            hideBanner();
            return;
        }

        // No consent yet — show banner and wire up buttons
        const acceptAll = document.getElementById('cookieAcceptAll');
        const acceptEssential = document.getElementById('cookieAcceptEssential');

        if (acceptAll) {
            acceptAll.addEventListener('click', function () {
                setConsent('all');
                hideBanner();
                loadThirdParty();
            });
        }

        if (acceptEssential) {
            acceptEssential.addEventListener('click', function () {
                setConsent('essential');
                hideBanner();
            });
        }
    }

    // Expose public API
    window.CookieConsent = {
        getConsent: getConsent,
        init: init
    };

    // Auto-initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
