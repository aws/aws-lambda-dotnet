

function onExecute() {
    var configFile = $("#config-file").val();
    var functionHandler = $("#functions-picker").val();

    var functionInvokeParameters = {}
    functionInvokeParameters.payload = $("#function-payload").val();
    functionInvokeParameters.profile = $("#aws-profile").val();
    functionInvokeParameters.region = $("#aws-region").val();


    $.post(
        {
            url: "webtester-api/Tester/" + configFile + "/" + functionHandler,
            data: JSON.stringify(functionInvokeParameters),
            contentType: "application/json",
            success: function (data) {

                $("#results-panel").show();

                var functionResponse = $("#function-response");
                if (data.isSuccess) {
                    functionResponse.css('color', 'black');
                    functionResponse.text(data.response);
                }
                else {
                    functionResponse.css('color', 'red');
                    functionResponse.text(data.error);
                }


                $("#function-logs").text(data.logs);
            }
        })
        .fail(function (data) {
            alert('Error sending execute request, is the .NET Lambda Test Tool still running?');
        });
}

function onPreSaveClick() {

    var currentSelectRequestname = $("#sample-requests").val();
    if(!currentSelectRequestname.startsWith("SavedRequests")) {
        $("#save-request-name").val("");
        return;
    }
    
    var displayValue = $("#sample-requests option:selected").text();
    $("#save-request-name").val(displayValue);
}

function onSaveRequest() {

    
    var request = $("#function-payload").val();
    var requestName = $("#save-request-name").val();
    if (!requestName) {
        $('#save-request-prompt').modal('hide');
        return;
    }
    
    $('#save-request-prompt').modal('hide');
    $("#save-request-name").val("");

    $.post(
        {
            url: "webtester-api/Tester/request/" + requestName,
            data: request,
            contentType: "text/plain",
            success: function (data) {
                var group = $("#saved-select-request-group");
                if (group.length) {

                    var exists = false;
                    $('#sample-requests').each(function(){
                        if (this.value === data) {
                            exists = true;
                            return false;
                        }
                    });
                    
                    if(!exists) {
                        group.append(`<option selected value="${data}">${requestName}</option>`);
                    }
                }
                else {
                    var voidOption = $("#void-select-request");
                    voidOption.after(`<optgroup id="saved-select-request-group" label="Saved Requests"><option selected value="${data}">${requestName}</option></optgroup>`);
                }
            }
        })
        .fail(function (data) {
            alert('Error saving request, is the .NET Lambda Test Tool still running?');
        });
}