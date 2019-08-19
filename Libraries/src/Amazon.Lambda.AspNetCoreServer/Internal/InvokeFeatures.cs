using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;

#pragma warning disable 1591

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    public class InvokeFeatures : IFeatureCollection,
                             IItemsFeature,
                             IHttpAuthenticationFeature,
                             IHttpRequestFeature,
                             IHttpResponseFeature,
                             IHttpConnectionFeature

#if !NETCOREAPP_2_1
                             ,IHttpResponseBodyFeature
#endif
    /*
    ,
                         IHttpUpgradeFeature,
                         IHttpRequestLifetimeFeature*/
    {

        public InvokeFeatures()
        {
            _features[typeof(IItemsFeature)] = this;
            _features[typeof(IHttpAuthenticationFeature)] = this;
            _features[typeof(IHttpRequestFeature)] = this;
            _features[typeof(IHttpResponseFeature)] = this;
            _features[typeof(IHttpConnectionFeature)] = this;
        }

#region IFeatureCollection
        public bool IsReadOnly => false;

        IDictionary<Type, object> _features = new Dictionary<Type, object>();

        public int Revision => 0;

        public object this[Type key]
        {
            get
            {
                object feature;
                if (_features.TryGetValue(key, out feature))
                {
                    return feature;
                }

                return null;
            }

            set
            {
                _features[key] = value;
            }
        }

        public TFeature Get<TFeature>()
        {
            object feature;
            if (_features.TryGetValue(typeof(TFeature), out feature))
            {
                return (TFeature)feature;
            }

            return default(TFeature);
        }

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator()
        {
            return this._features.GetEnumerator();
        }

        public void Set<TFeature>(TFeature instance)
        {
            if (instance == null)
                return;

            this._features[typeof(TFeature)] = instance;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this._features.GetEnumerator();
        }

#endregion
        
#region IItemsFeature
        IDictionary<object, object> IItemsFeature.Items { get; set; }
#endregion
        
#region IHttpAuthenticationFeature
        ClaimsPrincipal IHttpAuthenticationFeature.User { get; set; }
        
#if NETCOREAPP_2_1
        Microsoft.AspNetCore.Http.Features.Authentication.IAuthenticationHandler IHttpAuthenticationFeature.Handler { get; set; }
#endif
#endregion

#region IHttpRequestFeature
        string IHttpRequestFeature.Protocol { get; set; }

        string IHttpRequestFeature.Scheme { get; set; }

        string IHttpRequestFeature.Method { get; set; }

        string IHttpRequestFeature.PathBase { get; set; }

        string IHttpRequestFeature.Path { get; set; }

        string IHttpRequestFeature.QueryString { get; set; }

        string IHttpRequestFeature.RawTarget { get; set; }

        IHeaderDictionary IHttpRequestFeature.Headers { get; set; } = new HeaderDictionary();

        Stream IHttpRequestFeature.Body { get; set; } = new MemoryStream();

#endregion

#region IHttpResponseFeature
        int IHttpResponseFeature.StatusCode
        {
            get;
            set;
        }

        string IHttpResponseFeature.ReasonPhrase
        {
            get;
            set;
        }

        bool IHttpResponseFeature.HasStarted
        {
            get;
        }

        IHeaderDictionary IHttpResponseFeature.Headers
        {
            get;
            set;
        } = new HeaderDictionary();

        Stream IHttpResponseFeature.Body
        {
            get;
            set;
        } = new MemoryStream();

        internal EventCallbacks ResponseStartingEvents { get; private set; }
        void IHttpResponseFeature.OnStarting(Func<object, Task> callback, object state)
        {
            if (ResponseStartingEvents == null)
                this.ResponseStartingEvents = new EventCallbacks();

            this.ResponseStartingEvents.Add(callback, state);
        }

        internal EventCallbacks ResponseCompletedEvents { get; private set; }
        void IHttpResponseFeature.OnCompleted(Func<object, Task> callback, object state)
        {
            if (this.ResponseCompletedEvents == null)
                this.ResponseCompletedEvents = new EventCallbacks();

            this.ResponseCompletedEvents.Add(callback, state);
        }

        internal class EventCallbacks
        {
            List<EventCallback> _callbacks = new List<EventCallback>();

            internal void Add(Func<object, Task> callback, object state)
            {
                this._callbacks.Add(new EventCallback(callback, state));
            }

            internal async Task ExecuteAsync()
            {
                foreach(var callback in _callbacks)
                {
                    await callback.ExecuteAsync();
                }
            }

            internal class EventCallback
            {
                internal EventCallback(Func<object, Task> callback, object state)
                {
                    this.Callback = callback;
                    this.State = state;
                }

                Func<object, Task> Callback { get; }
                object State { get; }

                internal Task ExecuteAsync()
                {
                    var task = Callback(this.State);
                    return task;
                }
            }
        }

        #endregion

        #region IHttpResponseBodyFeature
#if !NETCOREAPP_2_1

#endif
        #endregion

        #region IHttpConnectionFeature

        string IHttpConnectionFeature.ConnectionId { get; set; }

        IPAddress IHttpConnectionFeature.RemoteIpAddress { get; set; }

        IPAddress IHttpConnectionFeature.LocalIpAddress { get; set; }

        int IHttpConnectionFeature.RemotePort { get; set; }

        int IHttpConnectionFeature.LocalPort { get; set; }

#endregion
    }
}
