using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;


// TODO: I see that there is some linking strategy to avoid this, but I cannot get it to work. Need advise.
[assembly: InternalsVisibleTo("Amazon.Lambda.Annotations.SourceGenerators.Tests")]

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    public class SqsMessageAttribute : Attribute, ISqsMessage, INotifyPropertyChanged
    {
        public const bool ContentBasedDeduplicationDefault = false;

        public const uint VisibilityTimeoutDefault = 30;
        internal const uint VisibilityTimeoutMinimum = 0;
        internal const uint VisibilityTimeoutMaximum = 43200;

        internal const uint EventBatchSizeMinimum = 1;
        internal const uint EventBatchSizeMaximum = 10000;
        public const uint EventBatchSizeDefault = 10;

        internal const string UintPropertyBetweenExceptionMessage = "{0} must be => {1} && <= {2}";

        public const uint DelaySecondsDefault = 0;
        public const bool FifoQueueDefault = false;
        public const uint DelaySecondsMinimum = 0;
        public const uint DelaySecondsMaximum = 900;

        public const uint KmsDataKeyReusePeriodSecondsDefault = 300;
        public const uint KmsDataKeyReusePeriodSecondsMinimum = 60;
        public const uint KmsDataKeyReusePeriodSecondsMaximum = 86400;


        internal const uint MaximumMessageSizeMinimum = 1024;
        internal const uint MaximumMessageSizeMaximum = 262144;
        public const uint MaximumMessageSizeDefault = 262144;

        internal const uint MessageRetentionPeriodMinimum = 60;
        internal const uint MessageRetentionPeriodMaximum = 345600;
        public const uint MessageRetentionPeriodDefault = 345600;
        // TODO: Make interpolated string when language version supports.  Current version does not support and I didn't want to make that change in a PR.
        internal const string MessageRetentionPeriodArgumentOutOfRangeExceptionMessage = "MessageRetentionPeriod must be => 60 && <= 345600";

        public const uint ReceiveMessageWaitTimeSecondsDefault = 0;
        internal const uint ReceiveMessageWaitTimeSecondsMinimum = 0;
        internal const uint ReceiveMessageWaitTimeSecondsMaximum = 20;
        // TODO: Make interpolated string when language version supports.  Current version does not support and I didn't want to make that change in a PR.
        internal const string ReceiveMessageWaitTimeSecondsArgumentOutOfRangeExceptionMessage = "ReceiveMessageWaitTimeSeconds must be => 0 && <= 20";
        internal const string RedriveAllowPolicyValidationNotValidJsonException = "Not valid JSON";

        private string _queueName;
        private string _queueLogicalId;
        private string _eventQueueArn;
        private string _deduplicationScope;
        private uint _delaySeconds = DelaySecondsDefault;
        private string _fifoThroughputLimit;
        private uint _kmsDataKeyReusePeriodSeconds = KmsDataKeyReusePeriodSecondsDefault;
        private uint _maximumMessageSize = MaximumMessageSizeDefault;
        private uint _messageRetentionPeriod = MessageRetentionPeriodDefault;
        private uint _eventBatchSize = EventBatchSizeDefault;
        private uint _visibilityTimeout = VisibilityTimeoutDefault;
        private uint _receiveMessageWaitTimeSeconds = ReceiveMessageWaitTimeSecondsDefault;
        private string _redrivePolicy;


        // event handler values
        public string EventQueueARN
        {
            get => _eventQueueArn;
            set
            {
                if(_eventQueueArn==value) return;
                _eventQueueArn = value;
                OnPropertyChanged();
            }
        }

        public uint EventBatchSize
        {
            get => _eventBatchSize;
            set
            {
                if (_eventBatchSize==value) return;
                if (value < EventBatchSizeMinimum || value > EventBatchSizeMaximum)
                {
                    throw new ArgumentOutOfRangeException(nameof(EventBatchSize), string.Format(UintPropertyBetweenExceptionMessage, nameof(EventBatchSize), EventBatchSizeMinimum, EventBatchSizeMaximum));
                }
                _eventBatchSize = value;
                OnPropertyChanged();
            }
        }

        public string QueueLogicalId
        {
            get => _queueLogicalId;
            set
            {
                if (_queueLogicalId == value) return;
                _queueLogicalId = value;
                OnPropertyChanged();
            }
        }

        // sqs queue values

        public string[] Tags { get; set; } = new string[] {};

        public uint VisibilityTimeout
        {
            get => _visibilityTimeout;
            set
            {
                if (value == _visibilityTimeout) return;
                if (value > VisibilityTimeoutMaximum)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(VisibilityTimeout), 
                        string.Format(UintPropertyBetweenExceptionMessage, nameof(VisibilityTimeout), VisibilityTimeoutMinimum, VisibilityTimeoutMaximum));
                }
                _visibilityTimeout = value;
                OnPropertyChanged();
            }
        }

        public uint ReceiveMessageWaitTimeSeconds
        {
            get => _receiveMessageWaitTimeSeconds;
            set
            {
                if ( value == _receiveMessageWaitTimeSeconds) return;
                if (value > ReceiveMessageWaitTimeSecondsMaximum)
                {
                    throw new ArgumentOutOfRangeException(nameof(ReceiveMessageWaitTimeSeconds), ReceiveMessageWaitTimeSecondsArgumentOutOfRangeExceptionMessage);
                }
                _receiveMessageWaitTimeSeconds = value;
                OnPropertyChanged();
            }
        }

        public bool ContentBasedDeduplication { get; set; } = ContentBasedDeduplicationDefault;

        public string DeduplicationScope
        {
            get => _deduplicationScope;
            set
            {
                if(_deduplicationScope==value) return;
                switch (value)
                {
                    case "messageGroup":
                    case "queue":
                    case "":
                    case null:
                        _deduplicationScope = value;
                        OnPropertyChanged();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(value);
                }
            }
        }

        public uint DelaySeconds
        {
            get => _delaySeconds;
            set
            {
                if (_delaySeconds==value) return;
                if (value > DelaySecondsMaximum)
                {
                    throw new ArgumentOutOfRangeException();
                }
                _delaySeconds = value;
                OnPropertyChanged();
            }
        }

        public bool FifoQueue { get; set; }

        public string FifoThroughputLimit
        {
            get => _fifoThroughputLimit;
            set
            {
                if(_fifoThroughputLimit==value ) return;
                switch (value)
                {
                    case "perMessageGroupId":
                    case "perQueue":
                    case "":
                    case null:
                        _fifoThroughputLimit = value;
                        OnPropertyChanged();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();

                }
            }
        }

        public uint KmsDataKeyReusePeriodSeconds
        {
            get => _kmsDataKeyReusePeriodSeconds;
            set
            {
                if (_kmsDataKeyReusePeriodSeconds==value) return;
                if (value < KmsDataKeyReusePeriodSecondsMinimum || value > KmsDataKeyReusePeriodSecondsMaximum)
                {
                    throw new ArgumentOutOfRangeException(nameof(KmsDataKeyReusePeriodSeconds), 
                        string.Format(UintPropertyBetweenExceptionMessage, 
                            nameof(KmsDataKeyReusePeriodSeconds), 
                            KmsDataKeyReusePeriodSecondsMinimum, 
                            KmsDataKeyReusePeriodSecondsMaximum));
                }
                _kmsDataKeyReusePeriodSeconds = value;
                OnPropertyChanged();
            }
        }


        public string KmsMasterKeyId { get; set; }

        public uint MaximumMessageSize
        {
            get => _maximumMessageSize;
            set
            {
                if (_maximumMessageSize == value) return;
                if (value < MaximumMessageSizeMinimum || value > MaximumMessageSizeMaximum)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaximumMessageSize), 
                        string.Format(UintPropertyBetweenExceptionMessage, nameof(MaximumMessageSize), MaximumMessageSizeMinimum, MaximumMessageSizeMaximum));
                }
                _maximumMessageSize = value;
                OnPropertyChanged();
            }
        }

        public uint MessageRetentionPeriod
        {
            get => _messageRetentionPeriod;
            set
            {
                if (_messageRetentionPeriod==value) return;
                if (value < MessageRetentionPeriodMinimum || value > MessageRetentionPeriodMaximum)
                {
                    throw new ArgumentOutOfRangeException(nameof(MessageRetentionPeriod), MessageRetentionPeriodArgumentOutOfRangeExceptionMessage);
                }
                _messageRetentionPeriod = value;
                OnPropertyChanged();
            }
        }

        public string RedriveAllowPolicy { get; set; }

        public string RedrivePolicy
        {
            get => _redrivePolicy;
            set
            {
                if (_redrivePolicy == value) return;
                _redrivePolicy = value;
                OnPropertyChanged();
            }
        }

        public string QueueName 
        {
            get => _queueName;
            set
            {
                if (_queueName==value) return;
                _queueName = value;
                this.OnPropertyChanged();
            }
        }

        
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            switch (propertyName)
            {
                case nameof(QueueLogicalId):
                case nameof(EventQueueARN):
                    if ((!string.IsNullOrEmpty(QueueLogicalId) || !string.IsNullOrEmpty(EventQueueARN)) && string.IsNullOrEmpty(QueueLogicalId) == string.IsNullOrEmpty(EventQueueARN))
                    {
                        throw new InvalidOperationException($"You can only specify one of: {nameof(QueueLogicalId)} or {nameof(EventQueueARN)}");
                    }
                    break;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
