using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BlueprintBaseName._1
{
    /// <summary>
    /// A utility class to store all the current values from the intent's slots.
    /// </summary>
    public class Reservation
    {
        public string ReservationType { get; set; }

        #region Car Reservation Fields
        public string PickUpCity { get; set; }
        public string PickUpDate { get; set; }
        public string ReturnDate { get; set; }
        public string CarType { get; set; }
        public string DriverAge { get; set; }

        [JsonIgnore]
        public bool HasRequiredCarFields
        {
            get
            {
                return !string.IsNullOrEmpty(PickUpCity)
                        && !string.IsNullOrEmpty(PickUpDate)
                        && !string.IsNullOrEmpty(ReturnDate)
                        && !string.IsNullOrEmpty(CarType)
                        && !string.IsNullOrEmpty(DriverAge);
            }
        }
        #endregion


        #region Hotel Resevation Fields

        public string CheckInDate { get; set; }
        public string Location { get; set; }
        public string Nights { get; set; }
        public string RoomType { get; set; }

        [JsonIgnore]
        public bool HasRequiredHotelFields
        {
            get
            {
                return !string.IsNullOrEmpty(CheckInDate)
                        && !string.IsNullOrEmpty(Location)
                        && !string.IsNullOrEmpty(Nights)
                        && !string.IsNullOrEmpty(RoomType);
            }
        }

        #endregion
    }
}
