// Google Drive explorer page: connect/sync forms, toasts, file table, search, type filters and view switcher.
(function ($) {
    'use strict';

    const KNOWN_TYPES = ['Folder', 'Spreadsheet', 'Document', 'PDF Document', 'Image', 'Presentation'];

    $(function () {
        function showToast(message) {
            const toastEl = document.getElementById('pageToast');
            if (!toastEl || !message) return;
            $('#pageToastMessage').text(message);
            bootstrap.Toast.getOrCreateInstance(toastEl, { delay: 4000 }).show();
        }

        // Success messages from the previous request (connect, sync, remove) show as a toast
        showToast($('.gd-page').data('status-message'));

        // Sample folder link
        $('#btnPasteSample').on('click', function () {
            $('#folderUrlInput').val('https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link').trigger('focus');
        });

        // Connect form spinner
        $('#driveExplorerForm').on('submit', function () {
            if ($(this).valid()) {
                $('#submitSpinner').removeClass('d-none');
                $('#submitText').text('Connecting...');
                $('#btnSubmitExplore').prop('disabled', true);
            }
        });

        // Sync form spinner
        $('#syncForm').on('submit', function () {
            $('#syncSpinner').removeClass('d-none');
            $('#syncIcon').addClass('d-none');
            $('#btnSyncDrive').prop('disabled', true);
        });

        // Copy folder ID
        $('.btn-copy-id').on('click', function () {
            const folderId = $(this).data('id');
            if (navigator.clipboard && folderId) {
                navigator.clipboard.writeText(folderId).then(function () {
                    showToast('Folder ID copied to clipboard.');
                });
            }
        });

        // File table. Columns: 0 Name, 1 Type, 2 Modified, 3 Size, 4 Actions
        let dataTable = null;
        if ($('#googleDriveDataTable').length > 0) {
            dataTable = $('#googleDriveDataTable').DataTable({
                pageLength: 25,
                lengthMenu: [[10, 25, 50, 100, -1], [10, 25, 50, 100, 'All']],
                order: [[2, 'desc']],
                autoWidth: false,
                columnDefs: [
                    { targets: 1, className: 'd-none d-md-table-cell' },
                    { targets: 2, className: 'd-none d-sm-table-cell' },
                    { targets: 3, className: 'd-none d-md-table-cell' },
                    { targets: 4, orderable: false, searchable: false }
                ],
                language: {
                    emptyTable: 'This folder is empty.',
                    zeroRecords: 'No files match your filters.',
                    info: '_START_–_END_ of _TOTAL_ files',
                    infoEmpty: 'No files',
                    infoFiltered: '',
                    lengthMenu: 'Show _MENU_'
                },
                // Search lives in the shared toolbar above, so only table, info, length and paging here
                dom: "<'row'<'col-12'tr>>" +
                     "<'row align-items-center mt-3 small text-muted'<'col-sm-12 col-md-4'i><'col-sm-12 col-md-3'l><'col-sm-12 col-md-5'p>>"
            });
        }

        // Shared search + type filter state for both the list and grid views
        let activeFilter = 'all';
        let searchTerm = '';

        function matchesType(fileType) {
            if (activeFilter === 'all') return true;
            if (activeFilter === 'other') return KNOWN_TYPES.indexOf(fileType) === -1;
            return fileType === activeFilter;
        }

        function applyFilters() {
            if (dataTable) {
                let typePattern = '';
                if (activeFilter === 'other') {
                    typePattern = '^(?!\\s*(' + KNOWN_TYPES.join('|') + ')\\s*$).*$';
                } else if (activeFilter !== 'all') {
                    typePattern = '^\\s*' + DataTable.util.escapeRegex(activeFilter) + '\\s*$';
                }
                dataTable.column(1).search(typePattern, true, false);
                dataTable.search(searchTerm).draw();
            }

            const term = searchTerm.toLowerCase();
            let visibleCards = 0;
            $('.grid-item-card').each(function () {
                const $card = $(this);
                const visible = matchesType($card.data('file-type')) && String($card.data('file-name')).indexOf(term) !== -1;
                $card.toggleClass('d-none', !visible);
                if (visible) visibleCards++;
            });
            $('#gridEmptyState').toggleClass('d-none', visibleCards > 0);
        }

        function setFilter(filter) {
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

        // "Show all spreadsheets" link in the featured section
        $('.js-show-filter').on('click', function () {
            setFilter($(this).data('filter'));
            document.getElementById('allFilesSection').scrollIntoView({ behavior: 'smooth', block: 'start' });
        });

        // List / grid switcher
        function setView(view) {
            const isTable = view === 'table';
            $('#tableViewSection').toggleClass('d-none', !isTable);
            $('#gridViewSection').toggleClass('d-none', isTable);
            $('#btnViewTable').toggleClass('active', isTable).attr('aria-pressed', String(isTable));
            $('#btnViewGrid').toggleClass('active', !isTable).attr('aria-pressed', String(!isTable));
            if (isTable && dataTable) dataTable.columns.adjust();
        }

        $('#btnViewTable').on('click', function () { setView('table'); });
        $('#btnViewGrid').on('click', function () { setView('grid'); });
    });
})(jQuery);
