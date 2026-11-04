using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Submission.KafkaProducers;

namespace UnitTests.Submission;

[Trait("Category", "UnitTests")]
public class SubmissionPatientStatusTests
{
    [Fact]
    public void TerminalErrorMessage_NormalMessage_UsesTypeAndMessage()
    {
        var message = SubmissionPatientStatus.TerminalErrorMessage(
            new InvalidOperationException("Failed to upload to external blob storage."),
            headers: null,
            fallback: null);

        Assert.Equal("InvalidOperationException: Failed to upload to external blob storage.", message);
    }

    [Fact]
    public void TerminalErrorMessage_MultiLineMessage_KeepsTheFirstLine()
    {
        var message = SubmissionPatientStatus.TerminalErrorMessage(
            new InvalidOperationException("Failed to upload to external blob storage.\r\n   at Submission.Listeners.SubmitPayloadListener"),
            headers: null,
            fallback: null);

        Assert.Equal("InvalidOperationException: Failed to upload to external blob storage.", message);
    }

    [Fact]
    public void TerminalErrorMessage_LongMessage_TruncatesTo200Characters()
    {
        var body = new string('x', 250);
        var message = SubmissionPatientStatus.TerminalErrorMessage(
            new InvalidOperationException(body),
            headers: null,
            fallback: null);

        var text = new string('x', 197) + "...";
        Assert.Equal(200, text.Length);
        Assert.Equal("InvalidOperationException: " + text, message);
    }

    [Theory]
    [InlineData("connection failed password=hunter2")]
    [InlineData("pwd=hunter2")]
    [InlineData("the Secret value")]
    [InlineData("access token rejected")]
    [InlineData("apiKey=abc")]
    [InlineData("api_key=abc")]
    [InlineData("accountKey=abc")]
    [InlineData("SharedAccessSignature=abc")]
    [InlineData("Authorization: Bearer abc")]
    public void TerminalErrorMessage_SecretMessage_UsesTypeOnly(string secret)
    {
        var message = SubmissionPatientStatus.TerminalErrorMessage(
            new InvalidOperationException(secret),
            headers: null,
            fallback: null);

        Assert.Equal("InvalidOperationException", message);
    }

    [Fact]
    public void TerminalErrorMessage_BlankMessage_UsesTypeOnly()
    {
        var message = SubmissionPatientStatus.TerminalErrorMessage(
            new InvalidOperationException(" \r\n   at Submission.Listeners.SubmitPayloadListener"),
            headers: null,
            fallback: null);

        Assert.Equal("InvalidOperationException", message);
    }

    [Fact]
    public void TerminalErrorMessage_BlankWithNoType_UsesSubmissionFailed()
    {
        Assert.Equal("Submission failed.", SubmissionPatientStatus.TerminalErrorMessage(null, null, " \r\n"));
        Assert.Equal("Submission failed.", SubmissionPatientStatus.TerminalErrorMessage(null, null, null));
    }

    [Fact]
    public void TerminalErrorMessage_SecretMessageWithNoType_UsesSubmissionFailed()
    {
        var headers = new Headers
        {
            {
                KafkaConstants.HeaderConstants.RetryExceptionMessage,
                Encoding.UTF8.GetBytes("connection failed password=hunter2\n   at Submission.Listener")
            }
        };

        var message = SubmissionPatientStatus.TerminalErrorMessage(
            new DeadLetterException("Retry count exceeded for message with key: abc"),
            headers,
            "Retry count exceeded for message with key: abc");

        Assert.Equal("Submission failed.", message);
        Assert.DoesNotContain("password", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hunter2", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TerminalErrorMessage_UnwrapsRetryWrapperToTheCause()
    {
        var headers = new Headers
        {
            {
                KafkaConstants.HeaderConstants.RetryExceptionMessage,
                Encoding.UTF8.GetBytes("stale header message\n   at Submission.Listener")
            }
        };
        var wrapper = new DeadLetterException(
            "Retry count exceeded for message with key: abc",
            new InvalidOperationException("disk full\r\n   at Submission.Listener"));

        var message = SubmissionPatientStatus.TerminalErrorMessage(wrapper, headers, wrapper.Message);

        Assert.Equal("InvalidOperationException: disk full", message);
    }

    [Fact]
    public void TerminalErrorMessage_UnwrapsListenerExecutionFailedException()
    {
        var wrapper = new ListenerExecutionFailedException(
            "listener failed",
            new InvalidOperationException("disk full"));

        var message = SubmissionPatientStatus.TerminalErrorMessage(wrapper, headers: null, fallback: wrapper.Message);

        Assert.Equal("InvalidOperationException: disk full", message);
    }

    [Fact]
    public void TerminalErrorMessage_RetryExhaustedWithoutACause_UsesExceptionTypeHeader()
    {
        var headers = new Headers
        {
            { "kafka_dlt-exception-fqcn", Encoding.UTF8.GetBytes("org.springframework.kafka.listener.ListenerExecutionFailedException") },
            { "kafka_dlt-exception-cause-fqcn", Encoding.UTF8.GetBytes("java.io.IOException") },
            {
                KafkaConstants.HeaderConstants.RetryExceptionMessage,
                Encoding.UTF8.GetBytes("disk full\n   at Submission.Listener")
            }
        };

        var message = SubmissionPatientStatus.TerminalErrorMessage(
            new DeadLetterException("Retry count exceeded for message with key: abc"),
            headers,
            "Retry count exceeded for message with key: abc");

        Assert.Equal("IOException: disk full", message);
    }

    [Fact]
    public void TerminalErrorMessage_CollapsesWhitespaceAndDropsControlCharacters()
    {
        var message = SubmissionPatientStatus.TerminalErrorMessage(
            new InvalidOperationException("Failed\tto   upload\u0001 now"),
            headers: null,
            fallback: null);

        Assert.Equal("InvalidOperationException: Failedto upload now", message);
    }

    private sealed class ListenerExecutionFailedException : Exception
    {
        public ListenerExecutionFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
