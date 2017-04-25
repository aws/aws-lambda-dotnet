using Amazon.Lambda.Core;
using Amazon.Lambda.LexEvents;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueprintBaseName
{
    public class BookHotelIntentProcessor : AbstractIntentProcessor
    {
        /// <summary>
        /// Performs dialog management and fulfillment for booking a hotel.
        /// 
        /// Beyond fulfillment, the implementation for this intent demonstrates the following:
        /// 1) Use of elicitSlot in slot validation and re-prompting
        /// 2) Use of sessionAttributes to pass information that can be used to guide conversation
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
                Location = slots.ContainsKey("Location") ? slots["Location"] : null,
                CheckInDate = slots.ContainsKey("CheckInDate") ? slots["CheckInDate"] : null,
                Nights = slots.ContainsKey("Nights") ? slots["Nights"] : null,
                RoomType = slots.ContainsKey("RoomType") ? slots["RoomType"] : null
            };

            sessionAttributes["currentReservation"] = SerializeReservation(reservation);

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

                    sessionAttributes["currentReservationPrice"] = price.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    sessionAttributes.Remove("currentReservationPrice");
                }



                return Delegate(sessionAttributes, slots);
            }

            // Booking the hotel.  In a real application, this would likely involve a call to a backend service.
            context.Logger.LogLine($"Book hotel at = {SerializeReservation(reservation)}");

            if (sessionAttributes.ContainsKey("currentReservationPrice"))
            {
                context.Logger.LogLine($"Book hotel price = {sessionAttributes["currentReservationPrice"]}");
            }

            sessionAttributes.Remove("currentReservationPrice");
            sessionAttributes.Remove("currentReservation");

            sessionAttributes["lastConfirmedReservation"] = SerializeReservation(reservation);

            return Close(
                        sessionAttributes,
                        "Fulfilled",
                        new LexResponse.LexMessage
                        {
                            ContentType = "PlainText",
                            Content = "Thanks, I have placed your hotel reservation."
                        }
                    );
        }


        /// <summary>
        /// Validated that any values for slots in the intent are valid values.
        /// </summary>
        /// <param name="reservation"></param>
        /// <returns></returns>
        private ValidationResult Validate(Reservation reservation)
        {
            if (!string.IsNullOrEmpty(reservation.Location) && !TypeValidators.IsValidCity(reservation.Location))
            {
                return new ValidationResult(false, "Location",
                    $"We currently do not support {reservation.Location} as a valid destination.  Can you try a different city?");
            }

            if (!string.IsNullOrEmpty(reservation.CheckInDate))
            {
                DateTime checkinDate = DateTime.MinValue;
                if (!DateTime.TryParse(reservation.CheckInDate, out checkinDate))
                {
                    return new ValidationResult(false, "CheckInDate",
                        "I did not understand your check in date.  When would you like to check in?");
                }
                if (checkinDate < DateTime.Today)
                {
                    return new ValidationResult(false, "CheckInDate",
                        "Reservations must be scheduled at least one day in advance.  Can you try a different date?");
                }
            }

            if (!string.IsNullOrEmpty(reservation.Nights))
            {
                int nights;
                if (!int.TryParse(reservation.Nights, out nights))
                {
                    return new ValidationResult(false, "Nights",
                        "I did not understand the number of nights.  Can you enter the number of nights again again?");
                }
                if (nights < 1 || nights > 30)
                {
                    return new ValidationResult(false, "Nights",
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
