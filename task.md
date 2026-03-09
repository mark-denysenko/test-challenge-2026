# test challenge 

Create two applications that will generate and process the data using RabbitMQ.

Technology stack:
- C#
- .NET 8
- MariaDB
- Entity Framework
- RabbitMQ
- xUnit for testing
- Docker

Application 1 (API) - Hash API
- REST API application
- Endpoint POST '/hashes' will generate 40.000 random SHA1 hashes and send them one by
one to RabbitMQ queue for further processing
- Endpoint GET '/hashes' will return a number of hashes from the database grouped by day
in JSON
Example of GET response
{
{
"hashes": [
"date": "2022-06-25",
"count": 471255631
},
{
"date": "2022-06-26",
"count": 822365413
},
{
"date": "2022-06-27",
"count": 1973565411
}
]
}

Application 2 - (Processor)
- Background worker application.
- Connect to RabbitMQ queue and process messages in parallel using 4 threads.
- Save message into database table 'hashes' (id, date, sha1).

Optional features to implement
- Split generated hashes into batches and send them into RabbitMQ in parallel.
- Retrieve hashes from the database without recalculating data on the fly.