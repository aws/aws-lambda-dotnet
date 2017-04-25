using Amazon.Lambda.Core;
using Amazon.Lambda.LexEvents;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueprintBaseName
{
    public class BookCarIntentProcessor : AbstractIntentProcessor
    {
        /// <summary>
        /// Performs dialog management and fulfillment for booking a car.
        /// 
        /// Beyond fulfillment, the implementation for this intent demonstrates the following:
        /// 1) Use of elicitSlot in slot validation and re-prompting
        /// 2) Use of sessionAttributes to pass information that can be used to guide conversation
        /// </summary>
        /// <param name="lexEvent"></param>
        /// <returns></returns>
        public override LexResponse Process(LexEvent lexEvent, ILambdaContext context)
        {

            var slots = lexEvent.CurrentIntent.Slots;
            var sessionAttributes = lexEvent.SessionAttributes ?? new Dictionary<string, string>();

            Reservation reservation = new Reservation
            {
                ReservationType = "Car",
                PickUpCity = slots.ContainsKey("PickUpCity") ? slots["PickUpCity"] : null,
                PickUpDate = slots.ContainsKey("PickUpDate") ? slots["PickUpDate"] : null,
                ReturnDate = slots.ContainsKey("ReturnDate") ? slots["ReturnDate"] : null,
                DriverAge = slots.ContainsKey("DriverAge") ? slots["DriverAge"] : null,
                CarType = slots.ContainsKey("CarType") ? slots["CarType"] : null,
            };

            string confirmationStaus = lexEvent.CurrentIntent.ConfirmationStatus;

            Reservation lastConfirmedReservation = null;
            if (slots.ContainsKey("lastConfirmedReservation"))
            {
                lastConfirmedReservation = DeserializeReservation(slots["lastConfirmedReservation"]);
            }

            string confirmationContext = sessionAttributes.ContainsKey("confirmationContext") ? sessionAttributes["confirmationContext"] : null;

            sessionAttributes["currentReservation"] = SerializeReservation(reservation);

            var validateResult = Validate(reservation);
            context.Logger.LogLine($"Has required fields: {reservation.HasRequiredCarFields}, Has valid values {validateResult.IsValid}");
            if(!validateResult.IsValid)
            {
                context.Logger.LogLine($"Slot {validateResult.ViolationSlot} is invalid: {validateResult.Message?.Content}");
            }

            if (reservation.HasRequiredCarFields && validateResult.IsValid)
            {
                var price = GeneratePrice(reservation);
                context.Logger.LogLine($"Generated price: {price}");

                sessionAttributes["currentReservationPrice"] = price.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                sessionAttributes.Remove("currentReservationPrice");
            }

            if (string.Equals(lexEvent.InvocationSource, "DialogCodeHook", StringComparison.Ordinal))
            {
                // If any slots are invalid, re-elicit for their value
                if (!validateResult.IsValid)
                {
                    slots[validateResult.ViolationSlot] = null;
                    return ElicitSlot(sessionAttributes, lexEvent.CurrentIntent.Name, slots, validateResult.ViolationSlot, validateResult.Message);
                }

                // Determine if the intent (and current slot settings) has been denied.  The messaging will be different
                // if the user is denying a reservation he initiated or an auto-populated suggestion.
                if (string.Equals(lexEvent.CurrentIntent.ConfirmationStatus, "Denied", StringComparison.Ordinal))
                {
                    sessionAttributes.Remove("confirmationContext");
                    sessionAttributes.Remove("currentReservation");

                    if (string.Equals(confirmationContext, "AutoPopulate", StringComparison.Ordinal))
                    {
                        return ElicitSlot(sessionAttributes,
                                            lexEvent.CurrentIntent.Name,
                                            new Dictionary<string, string>
                                            {
                                                {"PickUpCity", null },
                                                {"PickUpDate", null },
                                                {"ReturnDate", null },
                                                {"DriverAge", null },
                                                {"CarType", null }
                                            },
                                            "PickUpCity",
                                            new LexResponse.LexMessage
                                            {
                                                ContentType = "PlainText",
                                                Content = "Where would you like to make your car reservation?"
                                            }
                                        );
                    }

                    return Delegate(sessionAttributes, slots);
                }

                if (string.Equals(lexEvent.CurrentIntent.ConfirmationStatus, "None", StringComparison.Ordinal))
                {
                    // If we are currently auto-populating but have not gotten confirmation, keep requesting for confirmation.
                    if ((!string.IsNullOrEmpty(reservation.PickUpCity)
                        && !string.IsNullOrEmpty(reservation.PickUpDate)
                        && !string.IsNullOrEmpty(reservation.ReturnDate)
                        && !string.IsNullOrEmpty(reservation.DriverAge)
                        && !string.IsNullOrEmpty(reservation.CarType)) || string.Equals(confirmationContext, "AutoPopulate", StringComparison.Ordinal))
                    {
                        if (lastConfirmedReservation != null &&
                            string.Equals(lastConfirmedReservation.ReservationType, "Hotel", StringComparison.Ordinal))
                        {
                            // If the user's previous reservation was a hotel - prompt for a rental with
                            // auto-populated values to match this reservation.
                            sessionAttributes["confirmationContext"] = "AutoPopulate";
                            return ConfirmIntent(
                                    sessionAttributes,
                                    lexEvent.CurrentIntent.Name,
                                    new Dictionary<string, string>
                                    {
                                        {"PickUpCity", lastConfirmedReservation.PickUpCity },
                                        {"PickUpDate", lastConfirmedReservation.CheckInDate },
                                        {"ReturnDate", DateTime.Parse(lastConfirmedReservation.CheckInDate).AddDays(int.Parse(lastConfirmedReservation.Nights)).ToUniversalTime().ToString(CultureInfo.InvariantCulture) },
                                        {"CarType", null },
                                        {"DriverAge", null },
                                    },
                                    new LexResponse.LexMessage
                                    {
                                        ContentType = "PlainText",
                                        Content = $"Is this car rental for your {lastConfirmedReservation.Nights} night stay in {lastConfirmedReservation.Location} on {lastConfirmedReservation.CheckInDate}?"
                                    }
                                );
                        }
                    }

                    // Otherwise, let native DM rules determine how to elicit for slots and/or drive confirmation.
                    return Delegate(sessionAttributes, slots);
                }

                // If confirmation has occurred, continue filling any unfilled slot values or pass to fulfillment.
                if (string.Equals(lexEvent.CurrentIntent.ConfirmationStatus, "Confirmed", StringComparison.Ordinal))
                {
                    // Remove confirmationContext from sessionAttributes so it does not confuse future requests
                    sessionAttributes.Remove("confirmationContext");
                    if (string.Equals(confirmationContext, "AutoPopulate", StringComparison.Ordinal))
                    {
                        if (!string.IsNullOrEmpty(reservation.DriverAge))
                        {
                            return ElicitSlot(sessionAttributes,
                                                lexEvent.CurrentIntent.Name,
                                                slots,
                                                "DriverAge",
                                                new LexResponse.LexMessage
                                                {
                                                    ContentType = "PlainText",
                                                    Content = "How old is the driver of this car rental?"
                                                }
                                             );
                        }
                        else if (string.IsNullOrEmpty(reservation.CarType))
                        {
                            return ElicitSlot(sessionAttributes,
                                                lexEvent.CurrentIntent.Name,
                                                slots,
                                                "CarType",
                                                new LexResponse.LexMessage
                                                {
                                                    ContentType = "PlainText",
                                                    Content = "What type of car would you like? Popular models are economy, midsize, and luxury."
                                                }
                                             );
                        }
                    }

                    return Delegate(sessionAttributes, slots);
                }
            }

            context.Logger.LogLine($"Book car at = {SerializeReservation(reservation)}");

            if (sessionAttributes.ContainsKey("currentReservationPrice"))
            {
                context.Logger.LogLine($"Book car price = {sessionAttributes["currentReservationPrice"]}");
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
                            Content = "Thanks, I have placed your reservation."
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
            if (!string.IsNullOrEmpty(reservation.PickUpCity) && !TypeValidators.IsValidCity(reservation.PickUpCity))
            {
                return new ValidationResult(false, "PickupCity",
                    $"We currently do not support {reservation.PickUpCity} as a valid destination.  Can you try a different city?");
            }

            DateTime pickupDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(reservation.PickUpDate))
            {
                if (!DateTime.TryParse(reservation.PickUpDate, out pickupDate))
                {
                    return new ValidationResult(false, "PickUpDate",
                        "I did not understand your departure date.  When would you like to pick up your car rental?");
                }
                if (pickupDate < DateTime.Today)
                {
                    return new ValidationResult(false, "PickUpDate",
                        "Your pick up date is in the past!  Can you try a different date?");
                }
            }

            DateTime returnDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(reservation.ReturnDate))
            {
                if (!DateTime.TryParse(reservation.ReturnDate, out returnDate))
                {
                    return new ValidationResult(false, "ReturnDate",
                        "I did not understand your return date.  When would you like to return your car rental?");
                }
            }

            if (pickupDate != DateTime.MinValue && returnDate != DateTime.MinValue)
            {
                if (returnDate <= pickupDate)
                {
                    return new ValidationResult(false, "ReturnDate",
                        "Your return date must be after your pick up date.  Can you try a different return date?");
                }

                var ts = returnDate.Date - pickupDate.Date;
                if (ts.Days > 30)
                {
                    return new ValidationResult(false, "ReturnDate",
                        "You can reserve a car for up to thirty days.  Can you try a different return date?");
                }
            }

            int age = 0;
            if (!string.IsNullOrEmpty(reservation.DriverAge))
            {
                if (!int.TryParse(reservation.DriverAge, out age))
                {
                    return new ValidationResult(false, "DriverAge",
                        "I did not understand the driver's age.  Can you enter the driver's age again?");
                }
                if (age < 18)
                {
                    return new ValidationResult(false, "DriverAge",
                        "Your driver must be at least eighteen to rent a car.  Can you provide the age of a different driver?");
                }
            }

            if (!string.IsNullOrEmpty(reservation.CarType) && !TypeValidators.IsValidCarType(reservation.CarType))
            {
                return new ValidationResult(false, "CarType",
                    "I did not recognize that model.  What type of car would you like to rent?  " +
                    "Popular cars are economy, midsize, or luxury");
            }

            return ValidationResult.VALID_RESULT;
        }

        /// <summary>
        /// Generates a number within a reasonable range that might be expected for a flight.
        /// The price is fixed for a given pair of locations.
        /// </summary>
        /// <param name="reservation"></param>
        /// <returns></returns>
        private double GeneratePrice(Reservation reservation)
        {
            double baseLocationCost = 0;
            foreach (char c in reservation.PickUpCity)
            {
                baseLocationCost += (c - 97);
            }

            double ageMultiplier = int.Parse(reservation.DriverAge) < 25 ? 1.1 : 1.0;

            var carTypeIndex = 0;
            for (int i = 0; i < TypeValidators.VALID_CAR_TYPES.Length; i++)
            {
                if (string.Equals(TypeValidators.VALID_CAR_TYPES[i], reservation.CarType, StringComparison.Ordinal))
                {
                    carTypeIndex = i + 1;
                    break;
                }
            }

            int days = (DateTime.Parse(reservation.ReturnDate).Date - DateTime.Parse(reservation.PickUpDate).Date).Days;

            return days * ((100 + baseLocationCost) + ((carTypeIndex * 50) * ageMultiplier));
        }
    }
}
