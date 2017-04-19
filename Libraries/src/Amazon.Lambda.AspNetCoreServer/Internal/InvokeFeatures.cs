using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

#pragma warning disable 1591

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    public class InvokeFeatures : IFeatureCollection,
                             IHttpRequestFeature,
                             IHttpResponseFeature,
                             IHttpConnectionFeature
    /*
    ,
                         IHttpUpgradeFeature,
                         IHttpRequestLifetimeFeature*/
    {

        public InvokeFeatures()
        {
            _features[typeof(IHttpRequestFeature)] = this;
            _features[typeof(IHttpResponseFeature)] = this;
            _features[typeof(IHttpConnectionFeature)] = this;
        }

        #region IFeatureCollection
        public bool IsReadOnly => false;

        private int _featureRevision;
        IDictionary<Type, object> _features = new Dictionary<Type, object>();

        public int Revision => _featureRevision;

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

        void IHttpResponseFeature.OnStarting(Func<object, Task> callback, object state)
        {
        }

        void IHttpResponseFeature.OnCompleted(Func<object, Task> callback, object state)
        {
        }
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
