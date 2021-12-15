using Amazon.Lambda.Core;
using Amazon.Lambda.LexEvents;
using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace BlueprintBaseName._1
{
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
        /// <param name="lexEvent"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract LexResponse Process(LexEvent lexEvent, ILambdaContext context);

        protected string SerializeReservation(Reservation reservation)
        {
            return JsonConvert.SerializeObject(reservation, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        protected Reservation DeserializeReservation(string json)
        {
            return JsonConvert.DeserializeObject<Reservation>(json);
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

        protected LexResponse ElicitSlot(IDictionary<string, string> sessionAttributes, string intentName, IDictionary<string, string> slots, string slotToElicit, LexResponse.LexMessage message)
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

        protected LexResponse ConfirmIntent(IDictionary<string, string> sessionAttributes, string intentName, IDictionary<string, string> slots, LexResponse.LexMessage message)
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
}
