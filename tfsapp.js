var tfsSearch = {
    issueId: 0,

    // Add tfs from the item page
    add: function (id)
    {
        $("#tfs-add").click(function (e) {

            gemini_ui.startBusy('#tfs-add');

            var id = "";
            $("#table-search-tfs input[type='checkbox']:checked").each(
            function () {
                id = $(this).closest("td").attr("id") + "," + id;
            });

            gemini_ajax.postCall('apps/tfspicker', 'add', function (response) {
                if (response.Success) {
                    gemini_popup.toast("Added");
                    gemini_item.getAppControlValue(tfsSearch.issueId, '782D003D-D9F0-455F-AF09-74417D6DFD2B', '061F1F58-35BC-4509-83FC-CCC996ED4F36', 'add');
                }
                gemini_ui.stopBusy('#tfs-add');
            }, function () { gemini_ui.stopBusy('#tfs-add'); }, { issueId: tfsSearch.issueId, tfsId: id }, null, true);

        });

    },

    // Called when the content widget renders 
    init: function (id)
    {
        tfsSearch.issueId = id;
    },

    logout: function(id)
    {
        $('#tfspicker-logout').click(function () {
            gemini_ajax.postCall("apps/tfspicker", 'logout', function (response) {
                gemini_ui.startBusy('#tfspicker-logout');
                var issueid = tfsSearch.issueId;
                if (response.success) {
                    gemini_item.getAppControlValue(issueid, '782D003D-D9F0-455F-AF09-74417D6DFD2B', '061F1F58-35BC-4509-83FC-CCC996ED4F36', 'logout');
                }
                else {
                    if (response != '') gemini_popup.toast(response, true);
                }
                gemini_ui.stopBusy('#tfspicker-logout');
            }, function () { gemini_ui.stopBusy('#tfspicker-logout'); }, null, null, true);
        });
    },

    deletetfs: function (id)
    {
        $("#tfs-details").off('click', ".action-button-delete").on('click', ".action-button-delete", function (e) {


            var tfs = $(this).closest("span").attr("data-tfs");
            var issueid = tfsSearch.issueId;

            gemini_popup.modalConfirm("Are you sure to delete TFS ID-"+tfs+"?", null, function () {

                gemini_ui.startBusy('#modal-confirm #modal-button-yes');
                gemini_ajax.postCall('apps/tfspicker', 'delete', function (response) {
                    if (response.Success) {
                        gemini_popup.toast("Deleted");
                        gemini_item.getAppControlValue(issueid, '782D003D-D9F0-455F-AF09-74417D6DFD2B', '061F1F58-35BC-4509-83FC-CCC996ED4F36', 'delete');
                        gemini_ui.stopBusy('#modal-confirm #modal-button-yes');
                    }
                }, function () { gemini_ui.stopBusy('#modal-confirm #modal-button-yes');  }, { issueId: issueid, tfsRow: tfs }, null, true);

            });

        });
    },

    search: function (id)
    {
        $("#tfs-search").click(function (e) {

            gemini_ui.startBusy('#tfs-search');
            var match = $('#tfs-find').val();
            // do something
            gemini_ajax.postCall('apps/tfspicker', 'search', function (response) {
                if (response.Result.Data.success) {
                    $("#tfs-search-results").html(response.Result.Data.data.Content);

                    $('#tfs-details tbody tr').each(function (key, value) {
                        if ($('.tfs-' + $(this).attr('data-id'), $('#table-search-tfs')).length > 0) {
                            $('.tfs-' + $(this).attr('data-id'), $('#table-search-tfs')).remove();
                        }
                    });
                }
                else
                {
                    $('#app-tfspicker').html(response.Result.Data.data.Content);
                    $('#app-tfspicker #app-tfspicker-error').html(response.Result.Data.message);
                    $('#app-tfspicker-error').show();
                }
                gemini_item.setContentHeight();
                gemini_ui.stopBusy('#tfs-search');
            }, function () { gemini_ui.stopBusy('#tfs-search'); }, { id: id, search: match }, null, true);
        });
    },
};

//# sourceURL=tfsapp.js