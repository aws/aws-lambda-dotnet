using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.AlexaEvents
{
    /// <summary>
    /// Alexa Event
    /// </summary>
    public class AlexaEvent
    {
        /// <summary>
        /// The event version.
        /// </summary>
        public string EventVersion { get; set; }

        /// <summary>
        /// The session.
        /// </summary>
        public SessionRecord Session { get; set; }

        /// <summary>
        /// The request.
        /// </summary>
        public RequestRecord Request { get; set; }

        /// <summary>
        /// A session record.
        /// </summary>
        public class SessionRecord
        {
            /// <summary>
            /// The session id.
            /// </summary>
            public string SessionId { get; set; }

            /// <summary>
            /// The application.
            /// </summary>
            public Application Application { get; set; }

            /// <summary>
            /// The attributes.
            /// </summary>
            public Attribute Attributes { get; set; }

            /// <summary>
            /// The user.
            /// </summary>
            public User User { get; set; }

            /// <summary>
            /// The new flag.
            /// </summary>
            public bool New { get; set; }
        }

        /// <summary>
        /// A request record.
        /// </summary>
        public class RequestRecord
        {
            /// <summary>
            /// The type of request.
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// The request id.
            /// </summary>
            public string RequestId { get; set; }

            /// <summary>
            /// The locale.
            /// </summary>
            public string Locale { get; set; }

            /// <summary>
            /// The timestamp.
            /// </summary>
            public DateTime Timestamp { get; set; }

            /// <summary>
            /// The intent.
            /// </summary>
            public Intent Intent { get; set; }
        }

        /// <summary>
        /// An intent.
        /// </summary>
        public class Intent
        {
            /// <summary>
            /// Name of intent.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Slots for intent.
            /// </summary>
            public Slot Slots { get; set; }
        }

        /// <summary>
        /// A slot.
        /// </summary>
        public class Slot
        {
            /// <summary>
            /// The key.
            /// </summary>
            public string Key { get; set; }

            /// <summary>
            /// The value.
            /// </summary>
            public string Value { get; set; }
        }

        /// <summary>
        /// An application record.
        /// </summary>
        public class Application
        {
            /// <summary>
            /// The application id.
            /// </summary>
            public string ApplicationId { get; set; }
        }

        /// <summary>
        /// An attribute.
        /// </summary>
        public class Attribute
        {
            /// <summary>
            /// The type of attribute.
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// The value of attribute
            /// </summary>
            public string Value { get; set; }
        }

        /// <summary>
        /// A user.
        /// </summary>
        public class User
        {
            /// <summary>
            /// The user id.
            /// </summary>
            public string UserId { get; set; }
        }
    }
}
