using Amazon.Lambda.Core;
using Amazon.Lambda.LexEvents;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueprintBaseName._1
{
    public class BookCarIntentProcessor : AbstractIntentProcessor
    {
        public const string PICK_UP_CITY_SLOT = "PickUpCity";
        public const string PICK_UP_DATE_SLOT = "PickUpDate";
        public const string RETURN_DATE_SLOT = "ReturnDate";
        public const string DRIVER_AGE_SLOT = "DriverAge";
        public const string CAR_TYPE_SLOT = "CarType";




        /// <summary>
        /// Performs dialog management and fulfillment for booking a car.
        /// 
        /// Beyond fulfillment, the implementation for this intent demonstrates the following:
        /// 1) Use of elicitSlot in slot validation and re-prompting
        /// 2) Use of sessionAttributes to pass information that can be used to guide the conversation
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
                PickUpCity = slots.ContainsKey(PICK_UP_CITY_SLOT) ? slots[PICK_UP_CITY_SLOT] : null,
                PickUpDate = slots.ContainsKey(PICK_UP_DATE_SLOT) ? slots[PICK_UP_DATE_SLOT] : null,
                ReturnDate = slots.ContainsKey(RETURN_DATE_SLOT) ? slots[RETURN_DATE_SLOT] : null,
                DriverAge = slots.ContainsKey(DRIVER_AGE_SLOT) ? slots[DRIVER_AGE_SLOT] : null,
                CarType = slots.ContainsKey(CAR_TYPE_SLOT) ? slots[CAR_TYPE_SLOT] : null,
            };

            string confirmationStaus = lexEvent.CurrentIntent.ConfirmationStatus;

            Reservation lastConfirmedReservation = null;
            if (slots.ContainsKey(LAST_CONFIRMED_RESERVATION_SESSION_ATTRIBUTE))
            {
                lastConfirmedReservation = DeserializeReservation(slots[LAST_CONFIRMED_RESERVATION_SESSION_ATTRIBUTE]);
            }

            string confirmationContext = sessionAttributes.ContainsKey("confirmationContext") ? sessionAttributes["confirmationContext"] : null;

            sessionAttributes[CURRENT_RESERVATION_SESSION_ATTRIBUTE] = SerializeReservation(reservation);

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

                sessionAttributes[CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE] = price.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                sessionAttributes.Remove(CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE);
            }

            if (string.Equals(lexEvent.InvocationSource, "DialogCodeHook", StringComparison.Ordinal))
            {
                // If any slots are invalid, re-elicit for their value
                if (!validateResult.IsValid)
                {
                    slots[validateResult.ViolationSlot] = null;
                    return ElicitSlot(sessionAttributes, lexEvent.CurrentIntent.Name, slots, validateResult.ViolationSlot, validateResult.Message);
                }

                // Determine if the intent (and current slot settings) have been denied.  The messaging will be different
                // if the user is denying a reservation they initiated or an auto-populated suggestion.
                if (string.Equals(lexEvent.CurrentIntent.ConfirmationStatus, "Denied", StringComparison.Ordinal))
                {
                    sessionAttributes.Remove("confirmationContext");
                    sessionAttributes.Remove(CURRENT_RESERVATION_SESSION_ATTRIBUTE);

                    if (string.Equals(confirmationContext, "AutoPopulate", StringComparison.Ordinal))
                    {
                        return ElicitSlot(sessionAttributes,
                                            lexEvent.CurrentIntent.Name,
                                            new Dictionary<string, string>
                                            {
                                                {PICK_UP_CITY_SLOT, null },
                                                {PICK_UP_DATE_SLOT, null },
                                                {RETURN_DATE_SLOT, null },
                                                {DRIVER_AGE_SLOT, null },
                                                {CAR_TYPE_SLOT, null }
                                            },
                                            PICK_UP_CITY_SLOT,
                                            new LexResponse.LexMessage
                                            {
                                                ContentType = MESSAGE_CONTENT_TYPE,
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
                                        {PICK_UP_CITY_SLOT, lastConfirmedReservation.PickUpCity },
                                        {PICK_UP_DATE_SLOT, lastConfirmedReservation.CheckInDate },
                                        {RETURN_DATE_SLOT, DateTime.Parse(lastConfirmedReservation.CheckInDate).AddDays(int.Parse(lastConfirmedReservation.Nights)).ToUniversalTime().ToString(CultureInfo.InvariantCulture) },
                                        {CAR_TYPE_SLOT, null },
                                        {DRIVER_AGE_SLOT, null },
                                    },
                                    new LexResponse.LexMessage
                                    {
                                        ContentType = MESSAGE_CONTENT_TYPE,
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
                                                DRIVER_AGE_SLOT,
                                                new LexResponse.LexMessage
                                                {
                                                    ContentType = MESSAGE_CONTENT_TYPE,
                                                    Content = "How old is the driver of this car rental?"
                                                }
                                             );
                        }
                        else if (string.IsNullOrEmpty(reservation.CarType))
                        {
                            return ElicitSlot(sessionAttributes,
                                                lexEvent.CurrentIntent.Name,
                                                slots,
                                                CAR_TYPE_SLOT,
                                                new LexResponse.LexMessage
                                                {
                                                    ContentType = MESSAGE_CONTENT_TYPE,
                                                    Content = "What type of car would you like? Popular models are economy, midsize, and luxury."
                                                }
                                             );
                        }
                    }

                    return Delegate(sessionAttributes, slots);
                }
            }

            context.Logger.LogLine($"Book car at = {SerializeReservation(reservation)}");

            if (sessionAttributes.ContainsKey(CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE))
            {
                context.Logger.LogLine($"Book car price = {sessionAttributes[CURRENT_RESERVATION_PRICE_SESSION_ATTRIBUTE]}");
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
                            Content = "Thanks, I have placed your reservation."
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
            if (!string.IsNullOrEmpty(reservation.PickUpCity) && !TypeValidators.IsValidCity(reservation.PickUpCity))
            {
                return new ValidationResult(false, PICK_UP_CITY_SLOT,
                    $"We currently do not support {reservation.PickUpCity} as a valid destination.  Can you try a different city?");
            }

            DateTime pickupDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(reservation.PickUpDate))
            {
                if (!DateTime.TryParse(reservation.PickUpDate, out pickupDate))
                {
                    return new ValidationResult(false, PICK_UP_DATE_SLOT,
                        "I did not understand your departure date.  When would you like to pick up your car rental?");
                }
                if (pickupDate < DateTime.Today)
                {
                    return new ValidationResult(false, PICK_UP_DATE_SLOT,
                        "Your pick up date is in the past!  Can you try a different date?");
                }
            }

            DateTime returnDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(reservation.ReturnDate))
            {
                if (!DateTime.TryParse(reservation.ReturnDate, out returnDate))
                {
                    return new ValidationResult(false, RETURN_DATE_SLOT,
                        "I did not understand your return date.  When would you like to return your car rental?");
                }
            }

            if (pickupDate != DateTime.MinValue && returnDate != DateTime.MinValue)
            {
                if (returnDate <= pickupDate)
                {
                    return new ValidationResult(false, RETURN_DATE_SLOT,
                        "Your return date must be after your pick up date.  Can you try a different return date?");
                }

                var ts = returnDate.Date - pickupDate.Date;
                if (ts.Days > 30)
                {
                    return new ValidationResult(false, RETURN_DATE_SLOT,
                        "You can reserve a car for up to thirty days.  Can you try a different return date?");
                }
            }

            int age = 0;
            if (!string.IsNullOrEmpty(reservation.DriverAge))
            {
                if (!int.TryParse(reservation.DriverAge, out age))
                {
                    return new ValidationResult(false, DRIVER_AGE_SLOT,
                        "I did not understand the driver's age.  Can you enter the driver's age again?");
                }
                if (age < 18)
                {
                    return new ValidationResult(false, DRIVER_AGE_SLOT,
                        "Your driver must be at least eighteen to rent a car.  Can you provide the age of a different driver?");
                }
            }

            if (!string.IsNullOrEmpty(reservation.CarType) && !TypeValidators.IsValidCarType(reservation.CarType))
            {
                return new ValidationResult(false, CAR_TYPE_SLOT,
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
