using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Auth.AccessControlPolicy;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;

namespace Amazon.Lambda.Tools
{
    /// <summary>
    /// Utility class for interacting with console user to select or create an IAM role
    /// </summary>
    public class RoleHelper
    {
        public const int DEFAULT_ITEM_MAX = 20;
        private const int MAX_LINE_LENGTH_FOR_MANAGED_ROLE = 95;
        static readonly TimeSpan SLEEP_TIME_FOR_ROLE_PROPOGATION = TimeSpan.FromSeconds(15);
        public IAmazonIdentityManagementService IAMClient { get; private set; }

        public RoleHelper(IAmazonIdentityManagementService iamClient)
        {
            this.IAMClient = iamClient;
        }

        public string PromptForRole()
        {
            var existingRoles = FindExistingLambdaRoles(DEFAULT_ITEM_MAX);
            if (existingRoles.Count == 0)
            {
                return CreateRole();
            }

            var roleArn = SelectFromExisting(existingRoles);
            return roleArn;
        }

        private string SelectFromExisting(IList<Role> existingRoles)
        {
            Console.Out.WriteLine("Select IAM Role that Lambda will assume when executing function:");
            for (int i = 0; i < existingRoles.Count; i++)
            {
                Console.Out.WriteLine($"   {(i + 1).ToString().PadLeft(2)}) {existingRoles[i].RoleName}");
            }

            Console.Out.WriteLine($"   {(existingRoles.Count + 1).ToString().PadLeft(2)}) *** Create new IAM Role ***");
            Console.Out.Flush();

            int chosenIndex = WaitForIndexResponse(1, existingRoles.Count + 1);

            if (chosenIndex - 1 < existingRoles.Count)
            {
                return existingRoles[chosenIndex - 1].Arn;
            }
            else
            {
                return CreateRole();
            }
        }

        private string CreateRole()
        {
            Console.Out.WriteLine($"Enter name of the new IAM Role:");
            var roleName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(roleName))
                return null;

            roleName = roleName.Trim();

            Console.Out.WriteLine("Select IAM Policy to attach to the new role and grant permissions");

            var managedPolices = FindLambdaManagedPoliciesAsync(this.IAMClient, DEFAULT_ITEM_MAX).Result;
            for (int i = 0; i < managedPolices.Count; i++)
            {
                var line = $"   {(i + 1).ToString().PadLeft(2)}) {managedPolices[i].PolicyName}";

                var description = AttemptToGetPolicyDescription(managedPolices[i].Arn);
                if (!string.IsNullOrEmpty(description))
                {
                    if ((line.Length + description.Length) > MAX_LINE_LENGTH_FOR_MANAGED_ROLE)
                        description = description.Substring(0, MAX_LINE_LENGTH_FOR_MANAGED_ROLE - line.Length) + " ...";
                    line += $" ({description})";
                }

                Console.Out.WriteLine(line);
            }

            Console.Out.WriteLine($"   {(managedPolices.Count + 1).ToString().PadLeft(2)}) *** No policy, add permissions later ***");
            Console.Out.Flush();

            int chosenIndex = WaitForIndexResponse(1, managedPolices.Count + 1);

            string managedPolicyArn = null;
            if (chosenIndex < managedPolices.Count)
            {
                var selectedPolicy = managedPolices[chosenIndex - 1];
                managedPolicyArn = Constants.AWS_MANAGED_POLICY_ARN_PREFIX + selectedPolicy.Path + selectedPolicy.PolicyName;
            }

            string roleArn = CreateDefaultRole(roleName, managedPolicyArn);

            return roleArn;

        }

        private int WaitForIndexResponse(int min, int max)
        {
            int chosenIndex = -1;
            while (chosenIndex == -1)
            {
                var indexInput = Console.ReadLine()?.Trim();
                int parsedIndex;
                if (int.TryParse(indexInput, out parsedIndex) && parsedIndex >= min && parsedIndex <= max)
                {
                    chosenIndex = parsedIndex;
                }
                else
                {
                    Console.Out.WriteLine($"Invalid selection, must be a number between {min} and {max}");
                }
            }

            return chosenIndex;
        }

        private string ExpandRoleName(string roleName)
        {
            return ExpandRoleName(this.IAMClient, roleName);
        }

        public static string ExpandRoleName(IAmazonIdentityManagementService iamClient, string roleName)
        {
            if (roleName.StartsWith("arn:aws:iam"))
                return roleName;

            // Wrapping this in a task to avoid dealing with aggregate exception.
            var task = Task.Run<string>(async () =>
            {
                try
                {
                    var request = new GetRoleRequest { RoleName = roleName };
                    var response = await iamClient.GetRoleAsync(request).ConfigureAwait(false);
                    return response.Role.Arn;
                }
                catch (NoSuchEntityException)
                {
                    return null;
                }

            });

            if(task.Result == null)
            {
                throw new LambdaToolsException($"Role \"{roleName}\" can not be found.", LambdaToolsException.ErrorCode.RoleNotFound);
            }

            return task.Result;
        }




        public string CreateDefaultRole(string roleName, string managedRole)
        {
            string roleArn;
            try
            {
                CreateRoleRequest request = new CreateRoleRequest
                {
                    RoleName = roleName,
                    AssumeRolePolicyDocument = Constants.LAMBDA_ASSUME_ROLE_POLICY
                };

                var response = this.IAMClient.CreateRoleAsync(request).Result;
                roleArn = response.Role.Arn;
            }
            catch (Exception e)
            {
                throw new LambdaToolsException($"Error creating IAM Role: {e.Message}", LambdaToolsException.ErrorCode.IAMCreateRole, e);
            }

            if (!string.IsNullOrEmpty(managedRole))
            {
                try
                {
                    var request = new AttachRolePolicyRequest
                    {
                        RoleName = roleName,
                        PolicyArn = managedRole
                    };
                    this.IAMClient.AttachRolePolicyAsync(request).Wait();
                }
                catch (Exception e)
                {
                    throw new LambdaToolsException($"Error assigning managed IAM Policy: {e.Message}", LambdaToolsException.ErrorCode.IAMAttachRole, e);
                }
            }

            bool found = false;
            do
            {
                // There is no way check if the role has propagted yet so to
                // avoid error during function creation do a generous sleep.
                Console.WriteLine("Waiting for new IAM Role to propagate to AWS regions");
                long start = DateTime.Now.Ticks;
                while (TimeSpan.FromTicks(DateTime.Now.Ticks - start).TotalSeconds < SLEEP_TIME_FOR_ROLE_PROPOGATION.TotalSeconds)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    Console.Write(".");
                    Console.Out.Flush();
                }
                Console.WriteLine("\t Done");


                try
                {
                    var getResponse = this.IAMClient.GetRoleAsync(new GetRoleRequest { RoleName = roleName }).Result;
                    if (getResponse.Role != null)
                        found = true;
                }
                catch (NoSuchEntityException)
                {

                }
                catch (Exception e)
                {
                    throw new LambdaToolsException("Error confirming new role was created: " + e.Message, LambdaToolsException.ErrorCode.IAMGetRole, e);
                }
            } while (!found);


            return roleArn;
        }

        public static async Task<IList<ManagedPolicy>> FindLambdaManagedPoliciesAsync(IAmazonIdentityManagementService iamClient, int maxPolicies)
        {
            ListPoliciesRequest request = new ListPoliciesRequest
            {
                Scope = PolicyScopeType.AWS,
            };
            ListPoliciesResponse response = null;

            IList<ManagedPolicy> lambdaPolicies = new List<ManagedPolicy>();
            do
            {
                request.Marker = response?.Marker;
                response = await iamClient.ListPoliciesAsync(request).ConfigureAwait(false);

                foreach (var policy in response.Policies)
                {
                    if (policy.IsAttachable && (policy.PolicyName.StartsWith("AWSLambda") || string.Equals(policy.PolicyName, "PowerUserAccess", StringComparison.Ordinal)))
                        lambdaPolicies.Add(policy);

                    if (lambdaPolicies.Count == maxPolicies)
                        return lambdaPolicies;
                }

            } while (response.IsTruncated);

            response = await iamClient.ListPoliciesAsync(new ListPoliciesRequest
            {
                Scope = PolicyScopeType.Local
            });

            foreach (var policy in response.Policies)
            {
                if (policy.IsAttachable)
                    lambdaPolicies.Add(policy);

                if (lambdaPolicies.Count == maxPolicies)
                    return lambdaPolicies;
            }


            return lambdaPolicies;
        }

        public static async Task<IList<Role>> FindExistingLambdaRolesAsync(IAmazonIdentityManagementService iamClient, int maxRoles)
        {
            List<Role> roles = new List<Role>();

            ListRolesRequest request = new ListRolesRequest();
            ListRolesResponse response = null;
            do
            {
                if (response != null)
                    request.Marker = response.Marker;

                response = await iamClient.ListRolesAsync(request).ConfigureAwait(false);

                foreach (var role in response.Roles)
                {
                    if (AssumeRoleServicePrincipalSelector(role, "lambda.amazonaws.com"))
                    {
                        roles.Add(role);
                        if (roles.Count == maxRoles)
                        {
                            break;
                        }
                    }
                }

            } while (response.IsTruncated && roles.Count < maxRoles);

            return roles;
        }

        private IList<Role> FindExistingLambdaRoles(int maxRoles)
        {
            var task = Task.Run<IList<Role>>(async () =>
            {
                return await FindExistingLambdaRolesAsync(this.IAMClient, maxRoles);
            });

            return task.Result;
        }

        public static bool AssumeRoleServicePrincipalSelector(Role r, string servicePrincipal)
        {
            if (string.IsNullOrEmpty(r.AssumeRolePolicyDocument))
                return false;

            try
            {
                var decode = WebUtility.UrlDecode(r.AssumeRolePolicyDocument);
                var policy = Policy.FromJson(decode);
                foreach (var statement in policy.Statements)
                {
                    if (statement.Actions.Contains(new ActionIdentifier("sts:AssumeRole")) &&
                        statement.Principals.Contains(new Principal("Service", servicePrincipal)))
                    {
                        return true;
                    }
                }
                return r.AssumeRolePolicyDocument.Contains(servicePrincipal);
            }
            catch (Exception)
            {
                return false;
            }
        }


        static readonly Dictionary<string, string> KNOWN_MANAGED_POLICY_DESCRIPTIONS = new Dictionary<string, string>
        {
            {"arn:aws:iam::aws:policy/PowerUserAccess","Provides full access to AWS services and resources, but does not allow management of users and groups."},
            {"arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole","Provides write permissions to CloudWatch Logs."},
            {"arn:aws:iam::aws:policy/service-role/AWSLambdaDynamoDBExecutionRole","Provides list and read access to DynamoDB streams and write permissions to CloudWatch Logs."},
            {"arn:aws:iam::aws:policy/AWSLambdaExecute","Provides Put, Get access to S3 and full access to CloudWatch Logs."},
            {"arn:aws:iam::aws:policy/AWSLambdaFullAccess","Provides full access to Lambda, S3, DynamoDB, CloudWatch Metrics and Logs."},
            {"arn:aws:iam::aws:policy/AWSLambdaInvocation-DynamoDB","Provides read access to DynamoDB Streams."},
            {"arn:aws:iam::aws:policy/service-role/AWSLambdaKinesisExecutionRole","Provides list and read access to Kinesis streams and write permissions to CloudWatch Logs."},
            {"arn:aws:iam::aws:policy/AWSLambdaReadOnlyAccess","Provides read only access to Lambda, S3, DynamoDB, CloudWatch Metrics and Logs."},
            {"arn:aws:iam::aws:policy/service-role/AWSLambdaRole","Default policy for AWS Lambda service role."},
            {"arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole","Provides minimum permissions for a Lambda function to execute while accessing a resource within a VPC"}
        };

        /// <summary>
        /// Because description does not come back in the list policy operation cache known lambda policy descriptions to 
        /// help users understand which role to pick.
        /// </summary>
        /// <param name="policyArn"></param>
        /// <returns></returns>
        public string AttemptToGetPolicyDescription(string policyArn)
        {
            string content;
            if (!KNOWN_MANAGED_POLICY_DESCRIPTIONS.TryGetValue(policyArn, out content))
                return null;

            return content;
        }
    }
}
