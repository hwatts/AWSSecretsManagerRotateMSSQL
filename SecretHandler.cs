using System;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json;
using System.Data.SqlClient;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
using System.Collections.Generic;

namespace RotateMssql
{
    public class SecretsHandler
    {

        internal static void CreateSecret(AmazonSecretsManagerClient client, string secretId, string token)
        {
            Console.WriteLine("Creating secret");
            var secretValue = GetSecret(client, secretId, "AWSCURRENT");

            try {
                GetSecret(client, secretId, "AWSPENDING", token);
                Console.WriteLine("createSecret: There is already a pending secret for " + secretId + " - no need to set a new one");
            } catch (AggregateException ae) {
                ae.Handle((x) =>
                {
                    if (x is ResourceNotFoundException) // Catch the inner exception from the task and only handle the not found error
                    {
                        Console.WriteLine("createSecret: No pending secret found for " + secretId + " creating a new random password");
                        return true;
                    }
                    return false; // Let anything else fail the function.
                });
                var passwordRequest = new GetRandomPasswordRequest();
                passwordRequest.ExcludePunctuation = true; //avoid possible parsing issues
                var asyncGetPassword = client.GetRandomPasswordAsync(passwordRequest);
                string newPassword = asyncGetPassword.Result.RandomPassword;
                var secretString = JsonConvert.DeserializeObject<SecretString>(secretValue.SecretString);
                secretString.Password = newPassword;

                var asyncResult = client.PutSecretValueAsync(new PutSecretValueRequest{
                    SecretId = secretId,
                    ClientRequestToken = token,
                    SecretString = JsonConvert.SerializeObject(secretString),
                    VersionStages = new List<string> { "AWSPENDING" }
                });
                Console.WriteLine($"createSecret: Pending secret successfully set for {secretId} - " +
                                  $"response code: {asyncResult.Result.HttpStatusCode.ToString()}");
            }
        }

        internal static void SetSecret(AmazonSecretsManagerClient client, string secretId, string token)
        {
            Console.WriteLine("Attempting to retrieve secret value for AWSPENDING " + token);
            var pendingSecretValue = JsonConvert.DeserializeObject<SecretString>(GetSecret(client, secretId, "AWSPENDING", token).SecretString);
            Console.WriteLine("Attempting to retrieve secret value for AWSCURRENT");
            var currentSecretValue = JsonConvert.DeserializeObject<SecretString>(GetSecret(client, secretId, "AWSCURRENT").SecretString);
            string connectionString = 
                $"Server=tcp:{currentSecretValue.Host},{currentSecretValue.Port};Initial Catalog={currentSecretValue.DbName};Persist Security Info=True;"+
                $"User ID={currentSecretValue.Username};Password={currentSecretValue.Password}";
            //Using SMO as T-SQL methods forced to use dynamic SQL and had various possible SQL Injection routes
            var connection = new ServerConnection(new SqlConnection(connectionString));
            var server = new Server(connection);
            var logins = server.Logins;
            foreach (Login user in logins ){
                if (user.Name.ToLower() == currentSecretValue.Username.ToLower()){
                    user.ChangePassword(currentSecretValue.Password, pendingSecretValue.Password);
                    user.Alter();
                    user.Refresh();
                    Console.WriteLine($"Successfully changed password for username {currentSecretValue.Username}");
                    break;
                }
            }
        }

        internal static void TestSecret(AmazonSecretsManagerClient client, string secretId, string token)
        {   
            var pendingSecretValue = JsonConvert.DeserializeObject<SecretString>(GetSecret(client, secretId, "AWSPENDING", token).SecretString);
            string connectionString = 
                $"Server=tcp:{pendingSecretValue.Host},{pendingSecretValue.Port};Initial Catalog={pendingSecretValue.DbName};Persist Security Info=False;"+
                $"User ID={pendingSecretValue.Username};Password={pendingSecretValue.Password}";
            
            
            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand("SELECT GETDATE()", connection);
                connection.Open();
                int rowsAffected = command.ExecuteNonQuery();
                Console.WriteLine("testSecret: Successfully tested new password");
            }
            
        }
        internal static void FinishSecret(AmazonSecretsManagerClient client, string secretId, string token)
        {
            var secret = DescribeSecret(client, secretId);
            string currentVersion = "";
            foreach (var keyValue in secret.VersionIdsToStages){
                if (keyValue.Value.Contains("AWSCURRENT")){
                    currentVersion = keyValue.Key;
                    break;
                }
            }

            if (token == currentVersion) {
                Console.WriteLine("finishSecret: Version " + token + " already marked as AWSCURRENT for " + secretId);
                return;
            }

            var asyncMakeCurrentResult = client.UpdateSecretVersionStageAsync(new UpdateSecretVersionStageRequest
            {
                SecretId = secretId,
                VersionStage = "AWSCURRENT",
                MoveToVersionId = token,
                RemoveFromVersionId = currentVersion
            });
            Console.WriteLine($"finishSecret: Attempting to mark changed password as AWSCURRENT and removed AWSCURRENT label from {currentVersion}");
            // Access Result object to ensure processing completes synchronously
            Console.WriteLine("finishSecret: Call to set as current returned " + asyncMakeCurrentResult.Result.HttpStatusCode.ToString());

            var asyncRemovePendingResult = client.UpdateSecretVersionStageAsync(new UpdateSecretVersionStageRequest
            {
                SecretId = secretId,
                VersionStage = "AWSPENDING",
                RemoveFromVersionId = token
            });
            Console.WriteLine($"finishSecret: Attempting to remove AWSPENDING label from {token}");
            // Access Result object to ensure processing completes synchronously
            Console.WriteLine("Call to remove pending label returned HTTP code:" + asyncRemovePendingResult.Result.HttpStatusCode.ToString());

        }

        internal static GetSecretValueResponse GetSecret(AmazonSecretsManagerClient client, string secretId, string stage, string token = null)
        {
            
            var asyncResult = client.GetSecretValueAsync(
                new GetSecretValueRequest { 
                SecretId = secretId ,  VersionId = token,  VersionStage = stage }
            );
            return asyncResult.Result;

        }

        internal static DescribeSecretResponse DescribeSecret(AmazonSecretsManagerClient client, string secretId){

            var asyncResult = client.DescribeSecretAsync(new DescribeSecretRequest
                {
                    SecretId = secretId
                });

                return asyncResult.Result;
        }
    }
}
