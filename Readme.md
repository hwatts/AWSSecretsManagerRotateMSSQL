# SQL Server Single User Lambda handler for AWS Secrets Manager
Using the strategy described here:
https://docs.aws.amazon.com/secretsmanager/latest/userguide/rotating-secrets-one-user-one-password.html

# Build

install .net core SDK for your platform:
https://www.microsoft.com/net/download

install a recent version of the awscli that includes support for sam-template packaging:
https://aws.amazon.com/cli/

Restore dependencies
```
    dotnet restore
```


Package function into zip
```
    dotnet lambda package --FunctionName RotateMssql
```

# Deploy
Package sam template and copy zipped deployable to an S3 bucket:
```
    aws cloudformation package --template-file sam-template.yaml \
    --output-template-file output-sam-template.yaml --s3-bucket <yourS3Bucket>
```

Deploy sam template (creates a Cloudformation stack):
```
    aws cloudformation deploy --template-file output-sam-template.yaml \
    --stack-name RotateMSSQL --capabilities CAPABILITY_IAM \
    --parameter-overrides Subnets=<subnet-AZ1>,<subnet-AZ2>,<subnet-AZ3> SecurityGroupId=<security-group-id>
```

# Notes:

The password rotation itself is implemented with [SMO](https://docs.microsoft.com/en-us/sql/relational-databases/server-management-objects-smo/sql-server-management-objects-smo-programming-guide?view=sql-server-2017), specifically the [Login.ChangePassword(oldPassword,newPassword)](https://msdn.microsoft.com/en-us/library/ms208114.aspx) Method. This potentially has compatability issues with older versions of SQL Server, but the rationale for using it is that MS SQL doesn't allow the use of parameters on DDL statements, leaving significant risk of SQL Injection attacks when using ALTER LOGIN syntax directly. 

Currently, this only works with TCP connections to SQL Server over a known port, which works for RDS, but wouldn't work with named instances etc. Since this uses SMO, it's easy to adapt for wider compatibility - pull requests welcome.

The AWS Secret that invokes this must include the following initial values:

| Key     | Description                                                                                                                    |
| ------- | ------------------------------------------------------------------------------------------------------------------------------ |
|username | The username to enable rotation for                                                                                            |
|password | The current valid password - note that this will be used for initial sign-on, then immediately changed with a random string    |
|host     | The network name of the host to connect to (should work with IP or any network name that can be resolved in the VPC DNS)       |
|port     | The TCP port to connect to the instance on (1433 is the default for SQL Server)                                                |
|dbname   | The database name to connect to                                                                                                |
|engine   | Must be "sqlserver"                                                                                                            |

By default, SQL Logins have permission to change their own password, so this should work in most environments.

Although AWS Secrets Manager goes to some lengths to ensure that password changes are tested before they're commited and that the random password is stored in Secrets Manager before changing in the database, there is potential for a password to be rotated, but not promoted to AWSCURRENT in certain failure scenarios (mostly failure of the AWS Secrets Manager API itself). For this reason, I recommend not using this script to rotate the master password, or if you do, have a method of resetting the password outside of Secrets Manager in case of failure. 

This code should be considered sample code to modify and test in your environment. It has not been heavily tested for use in a production environment, so doing so is at your own risk.
