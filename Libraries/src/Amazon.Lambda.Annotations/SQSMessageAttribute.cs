using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Amazon.Lambda.Annotations.SourceGenerators.Tests")]

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    public class SqsMessageAttribute : Attribute, ISqsMessage, INotifyPropertyChanged
    {
        public const bool ContentBasedDeduplicationDefault = false;
        public const int VisibilityTimeoutDefault = 30;
        public const int BatchSizeDefault = 10;
        public const int DelaySecondsDefault = 0;
        public const bool FifoQueueDefault = false;
        public const int KmsDataKeyReusePeriodSecondsDefault = 300;
        public const int MessageRetentionPeriodDefault = 345600;
        public const int ReceiveMessageWaitTimeSecondsDefault = 0;
        public const int DelaySecondsMinimum = 0;
        public const int DelaySecondsMaximum = 900;
        public const int KmsDataKeyReusePeriodSecondsMinimum = 60;
        public const int KmsDataKeyReusePeriodSecondsMaximum = 86400;
        // TODO: Make interpolated string when language version supports.  Current version does not support and I didn't want to make that change in a PR.
        internal const string KmsDataKeyReusePeriodSecondsArgumentOutOfRangeExceptionMessage = "KmsDataKeyReusePeriodSeconds must be => 60 & <= 86400";

        public const int MaximumMessageSizeMinimum = 1024;
        public const int MaximumMessageSizeMaximum = 262144;
        public const int MaximumMessageSizeDefault = 262144;
        // TODO: Make interpolated string when language version supports.  Current version does not support and I didn't want to make that change in a PR.
        internal const string MaximumMessageSizeArgumentOutOfRangeExceptionMessage = "MaximumMessageSize must be => 1024 & <= 262144";

        private string _queueName;
        private string _queueLogicalId;
        private string _eventQueueArn;
        private string _deduplicationScope;
        private int _delaySeconds = DelaySecondsDefault;
        private string _fifoThroughputLimit;
        private int _kmsDataKeyReusePeriodSeconds = KmsDataKeyReusePeriodSecondsDefault;
        private int _maximumMessageSize = MaximumMessageSizeDefault;


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

        public int EventBatchSize { get; set; } = BatchSizeDefault;

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
        public int VisibilityTimeout { get; set; } = VisibilityTimeoutDefault;
        public int ReceiveMessageWaitTimeSeconds { get; set; } = ReceiveMessageWaitTimeSecondsDefault;
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

        public int DelaySeconds
        {
            get => _delaySeconds;
            set
            {
                if (_delaySeconds==value) return;
                if (value < DelaySecondsMinimum || value > DelaySecondsMaximum)
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

        public int KmsDataKeyReusePeriodSeconds
        {
            get => _kmsDataKeyReusePeriodSeconds;
            set
            {
                if (_kmsDataKeyReusePeriodSeconds==value) return;
                if (value < KmsDataKeyReusePeriodSecondsMinimum || value > KmsDataKeyReusePeriodSecondsMaximum)
                {
                    throw new ArgumentOutOfRangeException(nameof(KmsDataKeyReusePeriodSeconds), KmsDataKeyReusePeriodSecondsArgumentOutOfRangeExceptionMessage);
                }
                _kmsDataKeyReusePeriodSeconds = value;
                OnPropertyChanged();
            }
        }


        public string KmsMasterKeyId { get; set; }

        public int MaximumMessageSize
        {
            get => _maximumMessageSize;
            set
            {
                if (_maximumMessageSize == value) return;
                if (value < MaximumMessageSizeMinimum || value > MaximumMessageSizeMaximum)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaximumMessageSize), MaximumMessageSizeArgumentOutOfRangeExceptionMessage);
                }
                _maximumMessageSize = value;
                OnPropertyChanged();
            }
        }

        public int MessageRetentionPeriod { get; set; } = MessageRetentionPeriodDefault;
        public string RedriveAllowPolicy { get; set; }
        public string RedrivePolicy { get; set; }

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
