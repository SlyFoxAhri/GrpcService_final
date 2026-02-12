# GRPC service





Secure gRPC server built with .NET 10, MySQL and ASP.NET Core.



This project demonstrates how to implement general best practices in gRPC security and protection against the most common threats.



##### Overview:



The server connects to an example database called "junkyard" that displays information about the junkyards in Budapest. The user can use CRUD operations on the tables. All users are permitted to read data but, for create, update and delete authentication is required.



##### Tech stack:



.NET10 (not supported in VS2022 only in VS2026)

ASP.NET Core gRPC

MySQL (MySqlConnector)

JWT Authentication

TLS/HTTPS (Kestrel)





##### Security overview:



###### Hash + salt



* User credentials are always salted and hashed before they are stored in the database
* Login compares the stored hash with the provided hash
* This protects the used data even if the database becomes compromised



###### Token based authentication



* The API uses JWT (JSON Web Tokens) to authenticate clients,
* Validates issuer, audience, lifetime and signing key
* Token expiration is set to 60 minutes, after that refresh is required 
* Granular authorization is used before restricted operations with "\[Authorize]"



###### TLS encryption (HTTPS)



* Traffic is encrypted using TLS (Transport Layer Security)
* Enabled via built-in .NET development certificate (dotnet dev-certs https) and Kestrel endpoint configuration
* Prevents packet sniffing and man-in-middle attacks



###### Parameterised SQL



* Queries are prepared statements using parameterized commands
* Ensures user input is treated as data not an executable SQL
* Counters SQL injection



###### Input validation



* All user-provided data is validated before processing
* Checking for required fields, ensuring correct input type, length are within pre-defined ranges
* Prevents malformed request, accidental data corruption and injection attacks



###### Message size limiting



* Server enforces a maximum message size
* Stops resource exhaustion and Denial-of-Service attacks



###### Configuration



* Secrets and API keys are set as environment variables, never stored in appsettings.json
* Standard practice, sensitive data is not exposed to the public

