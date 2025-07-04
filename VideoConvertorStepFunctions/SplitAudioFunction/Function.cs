using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Util;
using System.IO;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Pipes;
using System.Runtime.InteropServices;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SplitAudioFunction;

public class Function
{
    IAmazonS3 S3Client { get; set; }

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        S3Client = new AmazonS3Client();
    }

    /// <summary>
    /// Constructs an instance with a preconfigured S3 client. This can be used for testing outside of the Lambda environment.
    /// </summary>
    /// <param name="s3Client">The service client to access Amazon S3.</param>
    public Function(IAmazonS3 s3Client)
    {
        this.S3Client = s3Client;
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="event">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task FunctionHandler(S3Event @event, ILambdaContext context)
    {
        context.Logger.LogInformation("function handler started");
        var eventRecords = @event.Records ?? new List<S3Event.S3EventNotificationRecord>();
        foreach (var record in eventRecords)
        {
            var s3Event = record.S3;
            if (s3Event == null)
            {
                continue;
            }

            try
            {
                var bucket = s3Event.Bucket.Name;
                var key = s3Event.Object.Key;

                context.Logger.LogInformation($"bucket:: {bucket}");
                context.Logger.LogInformation($"key:: {key}");

                var localVideoPath = $"/tmp/{Path.GetFileName(key)}";
                var localAudioPath = $"/tmp/{Path.GetFileNameWithoutExtension(key)}.aac";

                context.Logger.LogInformation($"Local video path: {localVideoPath}");
                context.Logger.LogInformation($"Local audio path: {localAudioPath}");

                // Download video file from S3
                using (var getObjectResponse = await S3Client.GetObjectAsync(bucket, key))
                using (var fileStream = File.Create(localVideoPath))
                {
                    await getObjectResponse.ResponseStream.CopyToAsync(fileStream);
                }

                if (!File.Exists(localVideoPath))
                {
                    context.Logger.LogError($"Failed to download video file from S3: {localVideoPath}");
                    throw new FileNotFoundException("Downloaded video file not found", localVideoPath);
                }

                context.Logger.LogInformation($"Video downloaded to {localVideoPath}");

                // Configure FFMpegCore binary folder
                var binaryFolder = GlobalFFOptions.Current.BinaryFolder;
                var ffmpegPath = Path.Combine(binaryFolder, GlobalFFOptions.GetFFMpegBinaryPath(GlobalFFOptions.Current));
                context.Logger.LogInformation($"ffmpeg Full Path: {ffmpegPath}");

                // Extract audio using FFMpegCore
                await FFMpegArguments
                    .FromFileInput(localVideoPath)
                    .OutputToFile(localAudioPath, true, options => options.WithAudioCodec("aac").DisableChannel(FFMpegCore.Enums.Channel.Video))
                    .Configure(options => options.BinaryFolder = "/opt/bin")
                    .ProcessAsynchronously();

                context.Logger.LogInformation($"Audio extracted to {localAudioPath}");
            }
            catch (Exception e)
            {
                context.Logger.LogError($"Error processing video: {e.Message}");
                context.Logger.LogError(e.StackTrace);
                throw;
            }
        }
    }
}