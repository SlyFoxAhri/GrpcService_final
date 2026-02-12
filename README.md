This is a gRPC servise server written in c#

Features: 
connects to a mysql database and can perform crud operations on said databae
anyone can read 
authenticaton requred to create upddate and delete

authentication is done with jwt token
user login credentioals are salted and hashed in the database
sql inputs are parameterised to prevent injection 
