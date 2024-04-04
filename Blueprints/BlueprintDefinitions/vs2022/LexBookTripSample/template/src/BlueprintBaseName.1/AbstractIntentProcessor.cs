﻿using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.LexEvents;

namespace BlueprintBaseName._1;

/// <summary>
/// Base class for intent processors.
/// </summary>
public abstract class AbstractIntentProcessor : IIntentProcessor
{
    internal const string CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE = "currentReservationPrice";
    internal const string CURRENT_RESERVATION_SESSION_ATTRIBUTE = "currentReservation";
    internal const string LAST_CONFIRMED_RESERVATION_SESSION_ATTRIBUTE = "lastConfirmedReservation";

    internal const string MESSAGE_CONTENT_TYPE = "PlainText";

    /// <summary>
    /// Main method for proccessing the lex event for the intent.
    /// </summary>
    /// <param name="lexEvent">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public abstract LexResponse Process(LexEvent lexEvent, ILambdaContext context);

    protected string SerializeReservation(Reservation reservation)
    {
        return JsonSerializer.Serialize(reservation, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    protected Reservation DeserializeReservation(string json)
    {
        return JsonSerializer.Deserialize<Reservation>(json) ?? new Reservation();
    }


    protected LexResponse Close(IDictionary<string, string> sessionAttributes, string fulfillmentState, LexResponse.LexMessage message)
    {
        return new LexResponse
        {
            SessionAttributes = sessionAttributes,
            DialogAction = new LexResponse.LexDialogAction
            {
                Type = "Close",
                FulfillmentState = fulfillmentState,
                Message = message
            }
        };
    }

    protected LexResponse Delegate(IDictionary<string, string> sessionAttributes, IDictionary<string, string> slots)
    {
        return new LexResponse
        {
            SessionAttributes = sessionAttributes,
            DialogAction = new LexResponse.LexDialogAction
            {
                Type = "Delegate",
                Slots = slots
            }
        };
    }

    protected LexResponse ElicitSlot(IDictionary<string, string> sessionAttributes, string intentName, IDictionary<string, string?> slots, string? slotToElicit, LexResponse.LexMessage? message)
    {
        return new LexResponse
        {
            SessionAttributes = sessionAttributes,
            DialogAction = new LexResponse.LexDialogAction
            {
                Type = "ElicitSlot",
                IntentName = intentName,
                Slots = slots,
                SlotToElicit = slotToElicit,
                Message = message
            }
        };
    }

    protected LexResponse ConfirmIntent(IDictionary<string, string> sessionAttributes, string intentName, IDictionary<string, string?> slots, LexResponse.LexMessage? message)
    {
        return new LexResponse
        {
            SessionAttributes = sessionAttributes,
            DialogAction = new LexResponse.LexDialogAction
            {
                Type = "ConfirmIntent",
                IntentName = intentName,
                Slots = slots,
                Message = message
            }
        };
    }
}