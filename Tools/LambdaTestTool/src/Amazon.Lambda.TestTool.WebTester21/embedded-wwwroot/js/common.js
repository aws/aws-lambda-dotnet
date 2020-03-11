$(document).ready(function () {
    onConfigFileChange();

    $('#monitor-dlq-tab').on('click', function (e) {
        onDlqTabActivated();
    });
});

function onAWSConfigChange() {

    var profile = $("#aws-profile").val();
    var region = $("#aws-region").val();

    console.log("Fetching queues for " + profile + "/" + region);
    var spinner = $("#spinnerDlqQueues");
    spinner.addClass("fa-spin");
    spinner.css("visibility", "visible");


    var select = $("#aws-dlq-queues");
    select.empty();

    if (profile && region) {
        $.get("webtester-api/MonitorDLQ/queues/" + profile + "/" + region,
            function (data) {


                for (i = 0; i < data.length; i++) {

                    select.append($('<option>',
                        {
                            value: data[i].queueUrl,
                            text: data[i].queueName
                        }));
                }

                spinner.removeClass("fa-spin");
                spinner.css("visibility", "hidden");
            });
    }
}


function onConfigFileChange() {
    var configFile = $("#config-file").val();
    console.log("Fetching lambda functions for config file " + configFile);

    $.get("webtester-api/Tester/" + configFile, function (data) {

        var profile = $("#aws-profile");
        profile.val(data.awsProfile);

        var region = $("#aws-region");
        region.val(data.awsRegion);

        var select = $("#functions-picker");
        select.empty();

        for (i = 0; i < data.functions.length; i++) {

            select.append($('<option>',
                {
                    value: data.functions[i].functionHandler,
                    text: data.functions[i].functionName
                }));
        }

        onAWSConfigChange();
    });
}


function onLambdaRequestSelected(data) {
    var requestName = $("#sample-requests").val();
    if (!requestName || requestName.startsWith("--"))
        return;

    $.get("webtester-api/Tester/request/" + requestName, function (data) {

        var functionPayload = $("#function-payload");
        functionPayload.val(data);
    });
}
