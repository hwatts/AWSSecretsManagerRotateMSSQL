using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace RotateMssql
{
    public class Function
    {

        /// <summary>
        /// A single user handler for AWS Secrets Manager for SQL Server
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public string FunctionHandler(RotateMssql.SecretsEvent input, ILambdaContext context)
        {
            Console.WriteLine("ClientRequestToken:" + input.ClientRequestToken);
            Console.WriteLine("SecretId:" + input.SecretId);
            Console.WriteLine("Step:" + input.Step);

            using (AmazonSecretsManagerClient client = new AmazonSecretsManagerClient())
            {
                var secret = SecretsHandler.DescribeSecret(client, input.SecretId);
               
                if  (!secret.RotationEnabled) {
                    Console.WriteLine("Secret " + secret.ARN + "is not enabled for rotation");
                    return "Secret " + secret.ARN + "is not enabled for rotation";
                }

                switch (input.Step)
                {
                    case "createSecret":
                        SecretsHandler.CreateSecret(client, input.SecretId, input.ClientRequestToken);
                        break;
                    case "setSecret":
                        SecretsHandler.SetSecret(client, input.SecretId, input.ClientRequestToken);
                        break;
                    case "testSecret":
                        SecretsHandler.TestSecret(client, input.SecretId, input.ClientRequestToken);
                        break;
                    case "finishSecret":
                        SecretsHandler.FinishSecret(client, input.SecretId, input.ClientRequestToken);
                        break;
                    default:
                        Console.WriteLine($"Handler not implemented for {input.Step}");
                        break;
                }

            }

            return "Success";
        }
    }
}
