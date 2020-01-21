function onDlqTabActivated() {

    $.get("webtester-api/MonitorDlq/is-running", function (data) {
        if (data) {


            $("#start-monitor-div").hide();
            $("#stop-monitor-div").show();

            if (!_pollingDlqLogs) {
                pollLogs();
            }
        }
        else {
            $("#start-monitor-div").show();
            $("#stop-monitor-div").hide();
        }
    });

}


function toggleMonitoringUI(showMonitoring) {

    if (showMonitoring) {
        $("#start-monitor-div").hide();
        $("#stop-monitor-div").show();
        $("#dlg-log-table-body").empty();
        $("#monitor-error-msg-div").hide();
    }
    else {
        $("#start-monitor-div").show();
        $("#stop-monitor-div").hide();
    }
}

var _currentLogs = [];

function resetDlqLogs() {
    this._currentLogs = [];
    $("#dlg-log-table-body").empty();
}

function appendLogs(serverLogs) {
    if (!Array.isArray(serverLogs)) {
        console.error("Return log data from server is not an array: " + JSON.stringify(serverLogs));
        return;
    }

    console.log(JSON.stringify(serverLogs));
    console.log(serverLogs.length + "logs returned from server");

    var body = $("#dlg-log-table-body");
    for (var i = 0; i < serverLogs.length; i++) {
        this._currentLogs.push(serverLogs[i]);

        var date = new Date(serverLogs[i].processTime);

        var formattedProcessTime = date.toLocaleTimeString();
        var formattedShortEvent = shortenForLogTable(serverLogs[i].event);
        if (areShortenAndFullDifferent(formattedShortEvent, serverLogs[i].event)) {
            formattedShortEvent = `<a href="javascript:onShowExpandedText(${this._currentLogs.length - 1}, 'event')">${formattedShortEvent}</a>`;
        }
        var formattedShortLogs = shortenForLogTable(serverLogs[i].logs);
        if (areShortenAndFullDifferent(formattedShortLogs, serverLogs[i].logs)) {
            formattedShortLogs = `<a href="javascript:onShowExpandedText(${this._currentLogs.length - 1}, 'logs')">${formattedShortLogs}</a>`;
        }
        var formattedShortError = shortenForLogTable(serverLogs[i].error);
        if (areShortenAndFullDifferent(formattedShortError, serverLogs[i].error)) {
            formattedShortError = `<a href="javascript:onShowExpandedText(${this._currentLogs.length - 1}, 'error')">${formattedShortError}</a>`;
        }

        var logHtml = `<tr><th></th><td>${formattedProcessTime}</td><td>${formattedShortLogs}</td><td>${formattedShortError}</td><td>${formattedShortEvent}</td></tr>`;
        body.prepend(logHtml);
    }
}

function onShowExpandedText(logIndex, type) {
    var logItem = this._currentLogs[logIndex];


    if (type === "event") {
        header = "Event";
        text = logItem.event;
    }
    else if (type === "logs") {
        header = "Logs";
        text = logItem.logs;
    }
    else if (type === "error") {
        header = "Error";
        text = logItem.error;
    }

    $("#dlq-expand-text-dialog-header").empty();
    $("#dlq-expand-text-dialog-header").append(`<div>${header}</div>`);
    $("#dlq-expand-text-dialog-text").empty();
    $("#dlq-expand-text-dialog-text").append(`<pre>${text}</pre>`);
    $('#dlq-expand-text-dialog').modal();
}

function areShortenAndFullDifferent(short, full) {
    if (short && short.trim() !== full.trim().replace(/"/g, '&quot;'))
        return true;

    return false;
}

function shortenForLogTable(text) {
    if (!text) {
        return "";
    }
    return text.replace("\n", "").substring(0, 50).replace(/"/g, '&quot;');
}

function onStartMonitoring() {
    var config = {};
    config.Profile = $("#aws-profile").val();
    config.Region = $("#aws-region").val();
    config.ConfigFile = $("#config-file").val()
    config.Function = $("#functions-picker").val();
    config.QueueUrl = $("#aws-dlq-queues").val();

    if(!config.QueueUrl) {
        $('#dlq-no-queue-selected-dialog').modal();
        return;
    }
    
    $.post(
        {
            url: "webtester-api/MonitorDLQ/start",
            data: JSON.stringify(config),
            contentType: "application/json",
            success: function (data) {

                toggleMonitoringUI(true);

                resetDlqLogs();
                pollLogs();
            }
        }).fail(function (data) {
            console.log("Error starting monitoring: " + data.statusText);

            var errorMessage;
            if (data.statusText === "error")
                errorMessage = "Error starting monitoring";
            else
                errorMessage = "Error starting monitoring: " + data.statusText;

            showMonitorErrorMessage(errorMessage);

            this._pollingDlqLogs = false;
            toggleMonitoringUI(false);
        });
}

function onStopMonitoring() {
    $.post(
        {
            url: "webtester-api/MonitorDLQ/stop",
            data: "{}",
            contentType: "application/json",
            success: function (data) {


                toggleMonitoringUI(false);
            }
        });
}

function isMonitoringDlq() {
    return $("#stop-monitor-div").is(":visible");
}

var _pollingDlqLogs = false;
function pollLogs() {
    this._pollingDlqLogs = true;
    $.get('webtester-api/MonitorDLQ/logs', function (data) {
        if (isMonitoringDlq()) {
            appendLogs(data);
            setTimeout(pollLogs, 500);
        }
        else {
            this._pollingDlqLogs = false;
        }
    }).fail(function (data) {
        console.log("Error fetching logs: " + data.statusText);

        var errorMessage;
        if (data.statusText === "error")
            errorMessage = "Monitoring Aborted";
        else
            errorMessage = "Error monitoring queue: " + data.statusText;

        showMonitorErrorMessage(errorMessage);

        this._pollingDlqLogs = false;
        toggleMonitoringUI(false);
    });
}

function showMonitorErrorMessage(errorMessage) {
    $("#monitor-error-msg-div").append(`<div>${errorMessage}</div>`);
    $("#monitor-error-msg-div").show();
}

function onPurgeDlqClick() {

    var queueUrl = $("#aws-dlq-queues").val();
    if(!queueUrl) {
        $('#dlq-no-queue-selected-dialog').modal();
        return;
    }
    
    $('#dlq-confirm-purge-dialog').modal();
}

function onConfirmedPurgeDlqClick() {
    $('#dlq-confirm-purge-dialog').modal('hide');

    var config = {};
    config.Profile = $("#aws-profile").val();
    config.Region = $("#aws-region").val();
    config.QueueUrl = $("#aws-dlq-queues").val();    

    $.post({
        url: "webtester-api/MonitorDLQ/purge",
        data: JSON.stringify(config),
        contentType: "application/json",
        success: function (data) {

        }
    })
    .fail(function (data) {
        alert('Error purging queue, is the .NET Lambda Test Tool still running?');
    });
}