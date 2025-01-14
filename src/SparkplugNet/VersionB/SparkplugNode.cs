// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SparkplugNode.cs" company="Hämmer Electronics">
// The project is licensed under the MIT license.
// </copyright>
// <summary>
//   Defines the SparkplugNode type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SparkplugNet.VersionB;

using SparkplugNet.Core;

/// <inheritdoc cref="SparkplugNodeBase{T}"/>
public class SparkplugNode : SparkplugNodeBase<VersionBData.Metric>
{
    /// <inheritdoc cref="SparkplugNodeBase{T}"/>
    /// <summary>
    /// Initializes a new instance of the <see cref="SparkplugNode"/> class.
    /// </summary>
    /// <param name="knownMetrics">The known metrics.</param>
    /// <param name="logger">The logger.</param>
    public SparkplugNode(List<VersionBData.Metric> knownMetrics, ILogger? logger = null) : base(knownMetrics, logger)
    {
    }

    /// <summary>
    /// Publishes version B metrics for a node.
    /// </summary>
    /// <param name="metrics">The metrics.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="Exception">An invalid metric type was specified.</exception>
    /// <returns>A <see cref="MqttClientPublishResult"/>.</returns>
    protected override async Task<MqttClientPublishResult> PublishMessage(List<VersionBData.Metric> metrics)
    {
        if (this.options is null)
        {
            throw new ArgumentNullException(nameof(this.options), "The options aren't set properly.");
        }

        if (this.KnownMetrics is not List<VersionBData.Metric> knownMetrics)
        {
            throw new Exception("Invalid metric type specified for version B metric.");
        }

        // Remove all not known metrics.
        metrics.RemoveAll(m => knownMetrics.FirstOrDefault(m2 => m2.Name == m.Name) == default);

        // Remove the session number metric if a user might have added it.
        metrics.RemoveAll(m => m.Name == Constants.SessionNumberMetricName);

        // Get the data message.
        var dataMessage = this.MessageGenerator.GetSparkPlugNodeDataMessage(
            this.NameSpace,
            this.options.GroupIdentifier,
            this.options.EdgeNodeIdentifier,
            metrics,
            this.LastSequenceNumber,
            this.LastSessionNumber,
            DateTimeOffset.Now);

        // Debug output.
        this.Logger?.Debug("NDATA Message: {@DataMessage}", dataMessage);

        // Increment the sequence number.
        this.IncrementLastSequenceNumber();

        // Publish the message.
        return await this.Client.PublishAsync(dataMessage);
    }

    /// <summary>
    /// Called when [message received].
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>
    /// A <see cref="T:System.Threading.Tasks.Task" /> representing any asynchronous operation.
    /// </returns>
    /// <exception cref="System.InvalidCastException">The metric cast didn't work properly.</exception>
    protected override Task OnMessageReceived(string topic, byte[] payload)
    {
        var payloadVersionB = PayloadHelper.Deserialize<VersionBProtoBuf.ProtoBufPayload>(payload);

        if (payloadVersionB is not null)
        {
            var convertedPayload = PayloadConverter.ConvertVersionBPayload(payloadVersionB);

            if (topic.Contains(SparkplugMessageType.DeviceCommand.GetDescription()))
            {
                if (convertedPayload is not VersionBData.Payload convertedPayloadVersionB)
                {
                    throw new InvalidCastException("The metric cast didn't work properly.");
                }

                foreach (var metric in convertedPayloadVersionB.Metrics)
                {
                    if (metric is VersionBData.Metric convertedMetric)
                    {
                        this.DeviceCommandReceived?.Invoke(convertedMetric);
                    }
                }
            }

            if (topic.Contains(SparkplugMessageType.NodeCommand.GetDescription()))
            {
                if (convertedPayload is not VersionBData.Payload convertedPayloadVersionB)
                {
                    throw new InvalidCastException("The metric cast didn't work properly.");
                }

                foreach (var metric in convertedPayloadVersionB.Metrics)
                {
                    if (metric is VersionBData.Metric convertedMetric)
                    {
                        this.NodeCommandReceived?.Invoke(convertedMetric);
                    }
                }
            }
        }

        return Task.CompletedTask;

    }
}
