using Amazon.Lambda.Core;
using Amazon.Lambda.LexEvents;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueprintBaseName._1
{
    public class BookHotelIntentProcessor : AbstractIntentProcessor
    {
        public const string LOCATION_SLOT = "Location";
        public const string CHECK_IN_DATE_SLOT = "CheckInDate";
        public const string NIGHTS_SLOT = "Nights";
        public const string ROOM_TYPE_SLOT = "RoomType";

        /// <summary>
        /// Performs dialog management and fulfillment for booking a hotel.
        /// 
        /// Beyond fulfillment, the implementation for this intent demonstrates the following:
        /// 1) Use of elicitSlot in slot validation and re-prompting
        /// 2) Use of sessionAttributes to pass information that can be used to guide the conversation
        /// </summary>
        /// <param name="lexEvent"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override LexResponse Process(LexEvent lexEvent, ILambdaContext context)
        {
            var slots = lexEvent.CurrentIntent.Slots;
            var sessionAttributes = lexEvent.SessionAttributes ?? new Dictionary<string, string>();

            Reservation reservation = new Reservation
            {
                ReservationType = "Hotel",
                Location = slots.ContainsKey(LOCATION_SLOT) ? slots[LOCATION_SLOT] : null,
                CheckInDate = slots.ContainsKey(CHECK_IN_DATE_SLOT) ? slots[CHECK_IN_DATE_SLOT] : null,
                Nights = slots.ContainsKey(NIGHTS_SLOT) ? slots[NIGHTS_SLOT] : null,
                RoomType = slots.ContainsKey(ROOM_TYPE_SLOT) ? slots[ROOM_TYPE_SLOT] : null
            };

            sessionAttributes[CURRENT_RESERVATION_SESSION_ATTRIBUTE] = SerializeReservation(reservation);

            if (string.Equals(lexEvent.InvocationSource, "DialogCodeHook", StringComparison.Ordinal))
            {
                var validateResult = Validate(reservation);
                // If any slots are invalid, re-elicit for their value
                if (!validateResult.IsValid)
                {
                    slots[validateResult.ViolationSlot] = null;
                    return ElicitSlot(sessionAttributes, lexEvent.CurrentIntent.Name, slots, validateResult.ViolationSlot, validateResult.Message);
                }

                // Otherwise, let native DM rules determine how to elicit for slots and prompt for confirmation.  Pass price
                // back in sessionAttributes once it can be calculated; otherwise clear any setting from sessionAttributes.
                if (reservation.HasRequiredHotelFields && validateResult.IsValid)
                {
                    var price = GeneratePrice(reservation);
                    context.Logger.LogLine($"Generated price: {price}");

                    sessionAttributes[CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE] = price.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    sessionAttributes.Remove(CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE);
                }



                return Delegate(sessionAttributes, slots);
            }

            // Booking the hotel.  In a real application, this would likely involve a call to a backend service.
            context.Logger.LogLine($"Book hotel at = {SerializeReservation(reservation)}");

            if (sessionAttributes.ContainsKey(CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE))
            {
                context.Logger.LogLine($"Book hotel price = {sessionAttributes[CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE]}");
            }

            sessionAttributes.Remove(CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE);
            sessionAttributes.Remove(CURRENT_RESERVATION_SESSION_ATTRIBUTE);

            sessionAttributes[LAST_CONFIRMED_RESERVATION_SESSION_ATTRIBUTE] = SerializeReservation(reservation);

            return Close(
                        sessionAttributes,
                        "Fulfilled",
                        new LexResponse.LexMessage
                        {
                            ContentType = MESSAGE_CONTENT_TYPE,
                            Content = "Thanks, I have placed your hotel reservation."
                        }
                    );
        }


        /// <summary>
        /// Verifies that any values for slots in the intent are valid.
        /// </summary>
        /// <param name="reservation"></param>
        /// <returns></returns>
        private ValidationResult Validate(Reservation reservation)
        {
            if (!string.IsNullOrEmpty(reservation.Location) && !TypeValidators.IsValidCity(reservation.Location))
            {
                return new ValidationResult(false, LOCATION_SLOT,
                    $"We currently do not support {reservation.Location} as a valid destination.  Can you try a different city?");
            }

            if (!string.IsNullOrEmpty(reservation.CheckInDate))
            {
                DateTime checkinDate = DateTime.MinValue;
                if (!DateTime.TryParse(reservation.CheckInDate, out checkinDate))
                {
                    return new ValidationResult(false, CHECK_IN_DATE_SLOT,
                        "I did not understand your check in date.  When would you like to check in?");
                }
                if (checkinDate < DateTime.Today)
                {
                    return new ValidationResult(false, CHECK_IN_DATE_SLOT,
                        "Reservations must be scheduled at least one day in advance.  Can you try a different date?");
                }
            }

            if (!string.IsNullOrEmpty(reservation.Nights))
            {
                int nights;
                if (!int.TryParse(reservation.Nights, out nights))
                {
                    return new ValidationResult(false, NIGHTS_SLOT,
                        "I did not understand the number of nights.  Can you enter the number of nights again again?");
                }
                if (nights < 1 || nights > 30)
                {
                    return new ValidationResult(false, NIGHTS_SLOT,
                        "You can make a reservations for from one to thirty nights.  How many nights would you like to stay for?");
                }
            }

            return ValidationResult.VALID_RESULT;
        }

        /// <summary>
        /// Generates a number within a reasonable range that might be expected for a hotel.
        /// The price is fixed for a pair of location and roomType.
        /// </summary>
        /// <param name="reservation"></param>
        /// <returns></returns>
        private double GeneratePrice(Reservation reservation)
        {
            double costOfLiving = 0;
            foreach (char c in reservation.Location)
            {
                costOfLiving += (c - 97);
            }

            int roomTypeIndex = 0;
            for (int i = 0; i < TypeValidators.VALID_ROOM_TYPES.Length; i++)
            {
                if (string.Equals(TypeValidators.VALID_ROOM_TYPES[i], reservation.CarType, StringComparison.Ordinal))
                {
                    roomTypeIndex = i + 1;
                    break;
                }
            }

            return int.Parse(reservation.Nights, CultureInfo.InvariantCulture) * (100 + costOfLiving + (100 + roomTypeIndex));
        }
    }

}
