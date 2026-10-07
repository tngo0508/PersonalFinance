// Google Drive explorer page: connect/sync forms, copy-to-clipboard, DataTables, view switcher and type filters.
(function ($) {
    'use strict';

    $(function () {
        // Paste Sample Handler
        $('#btnPasteSample').on('click', function () {
            $('#folderUrlInput').val('https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link');
        });

        // Form Submit Spinner
        $('#driveExplorerForm').on('submit', function () {
            if ($(this).valid()) {
                $('#submitSpinner').removeClass('d-none');
                $('#submitText').text('Connecting...');
                $('#btnSubmitExplore').prop('disabled', true);
            }
        });

        // Sync Form Spinner
        $('#syncForm').on('submit', function () {
            $('#syncSpinner').removeClass('d-none');
            $('#syncIcon').addClass('d-none');
            $('#btnSyncDrive').prop('disabled', true);
        });

        // Copy Folder ID Toast
        $('.btn-copy-id').on('click', function () {
            const folderId = $(this).data('id');
            if (navigator.clipboard && folderId) {
                navigator.clipboard.writeText(folderId).then(function () {
                    const toastEl = document.getElementById('copyToast');
                    if (toastEl) {
                        const toast = new bootstrap.Toast(toastEl, { delay: 2500 });
                        $('#copyToastMessage').text('Folder ID copied to clipboard: ' + folderId);
                        toast.show();
                    }
                });
            }
        });

        // Initialize DataTables with sorting by modifiedTime desc (column 5)
        let dataTable = null;
        if ($('#googleDriveDataTable').length > 0) {
            dataTable = $('#googleDriveDataTable').DataTable({
                responsive: true,
                pageLength: 25,
                lengthMenu: [[10, 25, 50, 100, -1], [10, 25, 50, 100, "All"]],
                order: [[5, 'desc']], // Default ordering: Column 5 (Last Modified) descending
                language: {
                    search: "",
                    searchPlaceholder: "Filter files in table...",
                    emptyTable: "No files found in this Google Drive folder."
                },
                dom: "<'row mb-2'<'col-sm-12 col-md-6'l><'col-sm-12 col-md-6'f>>" +
                     "<'row'<'col-sm-12'tr>>" +
                     "<'row mt-3'<'col-sm-12 col-md-5'i><'col-sm-12 col-md-7'p>>"
            });
        }

        // View Mode Switcher (Table vs Grid)
        $('#btnViewTable').on('click', function () {
            $('#btnViewTable').addClass('active btn-white shadow-sm').removeClass('text-secondary');
            $('#btnViewGrid').removeClass('active btn-white shadow-sm').addClass('text-secondary');
            $('#tableViewSection').removeClass('d-none');
            $('#gridViewSection').addClass('d-none');
        });

        $('#btnViewGrid').on('click', function () {
            $('#btnViewGrid').addClass('active btn-white shadow-sm').removeClass('text-secondary');
            $('#btnViewTable').removeClass('active btn-white shadow-sm').addClass('text-secondary');
            $('#gridViewSection').removeClass('d-none');
            $('#tableViewSection').addClass('d-none');
        });

        // Category Filter Chips Sync
        $('.gdrive-filter-chip').on('click', function () {
            $('.gdrive-filter-chip').removeClass('active');
            $(this).addClass('active');

            const category = $(this).data('filter');

            // 1. Filter DataTables
            if (dataTable) {
                if (category === 'all') {
                    dataTable.column(2).search('').draw();
                } else if (category === 'other') {
                    dataTable.column(2).search('^(?!\\s*(Folder|Spreadsheet|Document|PDF Document|Image|Presentation)\\s*$).*$', true, false).draw();
                } else {
                    const escapedCat = category.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
                    dataTable.column(2).search('^\\s*' + escapedCat + '\\s*$', true, false).draw();
                }
            }

            // 2. Filter Grid Cards
            if (category === 'all') {
                $('.grid-item-card').show();
            } else if (category === 'other') {
                $('.grid-item-card').each(function () {
                    const itemType = $(this).data('file-type');
                    if (itemType !== 'Folder' && itemType !== 'Spreadsheet' && itemType !== 'Document' && itemType !== 'PDF Document' && itemType !== 'Image' && itemType !== 'Presentation') {
                        $(this).show();
                    } else {
                        $(this).hide();
                    }
                });
            } else {
                $('.grid-item-card').each(function () {
                    const itemType = $(this).data('file-type');
                    if (itemType === category) {
                        $(this).show();
                    } else {
                        $(this).hide();
                    }
                });
            }
        });
    });
})(jQuery);
