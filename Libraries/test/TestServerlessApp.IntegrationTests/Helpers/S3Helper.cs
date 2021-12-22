using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace TestServerlessApp.IntegrationTests.Helpers
{
    public class S3Helper
    {
        private readonly IAmazonS3 _s3Client;

        public S3Helper(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }

        public async Task DeleteBucketAsync(string bucketName)
        {
            if (!await BucketExistsAsync(bucketName))
                return;

            var response = await _s3Client.ListObjectsAsync(new ListObjectsRequest{BucketName = bucketName});
            foreach (var s3Object in response.S3Objects)
            {
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest {BucketName = bucketName, Key = s3Object.Key});
            }

            await _s3Client.DeleteBucketAsync(new DeleteBucketRequest {BucketName = bucketName});
        }

        public async Task<bool> BucketExistsAsync(string bucketName)
        {
            var response = await _s3Client.ListBucketsAsync(new ListBucketsRequest());
            return response.Buckets.Any(x => x.BucketName.Equals(bucketName));
        }
    }
}