/* global PFI18n */
// Google Drive explorer page: connect/sync forms, toasts, file table, search, type filters and view switcher.
(function ($) {
    'use strict';

    const KNOWN_TYPES = ['Folder', 'Spreadsheet', 'Document', 'PDF Document', 'Image', 'Presentation'];

    $(() => {
        function showToast (message) {
            const toastEl = document.getElementById('pageToast');
            if (!toastEl || !message) { return; }
            $('#pageToastMessage').text(message);
            bootstrap.Toast.getOrCreateInstance(toastEl, { delay: 4000 }).show();
        }

        // Success messages from the previous request (connect, sync, remove) show as a toast
        showToast($('.gd-page').data('status-message'));

        // Connect form spinner
        $('#driveExplorerForm').on('submit', function () {
            if ($(this).valid()) {
                $('#submitSpinner').removeClass('d-none');
                $('#submitText').text(PFI18n.t('GoogleDrive.Js.Connecting'));
                $('#btnSubmitExplore').prop('disabled', true);
            }
        });

        // Sync form spinner
        $('#syncForm').on('submit', () => {
            $('#syncSpinner').removeClass('d-none');
            $('#syncIcon').addClass('d-none');
            $('#btnSyncDrive').prop('disabled', true);
        });

        // Fallback sync form spinner (error state)
        $('#syncFormFallback').on('submit', () => {
            $('#syncSpinnerFallback').removeClass('d-none');
            $('#btnSyncDriveFallback').prop('disabled', true);
        });

        // Copy folder ID
        $('.btn-copy-id').on('click', async function () {
            const folderId = $(this).data('id');
            if (navigator.clipboard && folderId) {
                try {
                    await navigator.clipboard.writeText(folderId);
                    showToast(PFI18n.t('GoogleDrive.Js.FolderIdCopied'));
                } catch {
                    showToast(PFI18n.t('GoogleDrive.Js.FolderIdCopyFailed'));
                }
            }
        });

        // Keyboard shortcut: Ctrl+K / Cmd+K to focus search
        $(document).on('keydown', (e) => {
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                const $search = $('#fileSearch');
                if ($search.length) {
                    $search.focus();
                }
            }
        });

        // File table. Columns: 0 Name, 1 Type, 2 Modified, 3 Size, 4 Actions.
        // Page size is fixed at 25: search and the type chips narrow the list, so there is no page-length select.
        let dataTable = null;
        if ($('#googleDriveDataTable').length > 0) {
            dataTable = $('#googleDriveDataTable').DataTable({
                pageLength: 25,
                lengthChange: false,
                order: [[2, 'desc']],
                autoWidth: false,
                columnDefs: [
                    { targets: 1, className: 'd-none d-md-table-cell' },
                    { targets: 2, className: 'd-none d-sm-table-cell' },
                    { targets: 3, className: 'd-none d-md-table-cell' },
                    { targets: 4, orderable: false, searchable: false }
                ],
                // DataTables fills in its own _START_, _END_ and _TOTAL_ tokens
                language: {
                    emptyTable: PFI18n.t('GoogleDrive.Js.Files.Empty'),
                    zeroRecords: PFI18n.t('GoogleDrive.Js.Files.NoMatches'),
                    info: PFI18n.t('GoogleDrive.Js.Files.Info', { start: '_START_', end: '_END_', total: '_TOTAL_' }),
                    infoEmpty: PFI18n.t('GoogleDrive.Js.Files.InfoEmpty'),
                    infoFiltered: ''
                },
                // Search lives in the shared toolbar above, so only table, info and paging here
                dom: "<'row'<'col-12'tr>>" +
                     "<'row dt-layout-row mt-3'<'col-sm-12 col-md-6 d-flex align-items-center gap-2'i><'col-sm-12 col-md-6 d-flex justify-content-md-end align-items-center'p>>",
                // A pager with one page offers nothing to click, so it shows only when the list spans pages
                drawCallback () {
                    const api = this.api();
                    $(api.table().container()).find('.dt-paging').toggleClass('d-none', api.page.info().pages <= 1);
                }
            });
        }

        // Shared search + type filter state for both the list and grid views
        let activeFilter = 'all';
        let searchTerm = '';

        function matchesType (fileType) {
            if (activeFilter === 'all') { return true; }
            if (activeFilter === 'other') { return KNOWN_TYPES.indexOf(fileType) === -1; }
            return fileType === activeFilter;
        }

        // Update the results count indicator
        function updateResultsCount (tableCount, gridCount) {
            const $count = $('#fileResultsCount');
            const hasFilter = activeFilter !== 'all' || searchTerm.length > 0;

            if (!hasFilter) {
                $count.addClass('d-none').text('');
                return;
            }

            const count = tableCount !== undefined ? tableCount : gridCount;
            if (count !== undefined && count >= 0) {
                $count.removeClass('d-none').text(PFI18n.plural('GoogleDrive.Js.Files.Found', count));
            } else {
                $count.addClass('d-none').text('');
            }
        }

        function applyFilters () {
            if (dataTable) {
                let typePattern = '';
                if (activeFilter === 'other') {
                    typePattern = `^(?!\\s*(${ KNOWN_TYPES.join('|') })\\s*$).*$`;
                } else if (activeFilter !== 'all') {
                    typePattern = `^\\s*${ DataTable.util.escapeRegex(activeFilter) }\\s*$`;
                }
                dataTable.column(1).search(typePattern, true, false);
                dataTable.search(searchTerm).draw();
                // Update results count from DataTable
                const tableCount = dataTable.page.info().recordsDisplay;
                updateResultsCount(tableCount);
            }

            const term = searchTerm.toLowerCase();
            let visibleCards = 0;
            $('.grid-item-card').each(function () {
                const $card = $(this);
                const visible = matchesType($card.data('file-type')) && String($card.data('file-name')).indexOf(term) !== -1;
                $card.toggleClass('d-none', !visible);
                if (visible) { visibleCards++; }
            });
            $('#gridEmptyState').toggleClass('d-none', visibleCards > 0);

            // If grid view is active, update count from grid
            if (!$('#gridViewSection').hasClass('d-none')) {
                updateResultsCount(undefined, visibleCards);
            }
        }

        function setFilter (filter) {
            activeFilter = filter;
            $('.gdrive-filter-chip').each(function () {
                const isActive = $(this).data('filter') === filter;
                $(this).toggleClass('active', isActive).attr('aria-pressed', String(isActive));
            });
            applyFilters();
        }

        $('.gdrive-filter-chip').on('click', function () {
            setFilter($(this).data('filter'));
        });

        $('#fileSearch').on('input', function () {
            searchTerm = $(this).val().trim();
            applyFilters();
        });

        // "Show all N spreadsheets" reveals the cards past the first six: the cards hold each sheet's actions
        $('.js-show-all-sheets').on('click', function () {
            const $extra = $('.gd-sheet-extra');
            $extra.removeClass('d-none');
            $(this).attr('aria-expanded', 'true').addClass('d-none');
            $extra.first().find('button').first().trigger('focus');
        });

        // List / grid switcher
        function setView (view) {
            const isTable = view === 'table';
            $('#tableViewSection').toggleClass('d-none', !isTable);
            $('#gridViewSection').toggleClass('d-none', isTable);
            $('#btnViewTable').toggleClass('active', isTable).attr('aria-pressed', String(isTable));
            $('#btnViewGrid').toggleClass('active', !isTable).attr('aria-pressed', String(!isTable));
            if (isTable && dataTable) { dataTable.columns.adjust(); }
            // Recalculate results count for the active view
            applyFilters();
        }

        $('#btnViewTable').on('click', () => { setView('table'); });
        $('#btnViewGrid').on('click', () => { setView('grid'); });
    });
})(jQuery);
