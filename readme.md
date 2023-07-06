Simple micro-service for saving high-scores in redis. With free to use node in case you don't want to host it yourself

# Hosting
This project is build to use with Docker, but any other ASP.NET Core hosting solutions would be fine. To start you need to setup Redis database and provide connection string in format of StackExchange.Redis [connection strings](https://stackexchange.github.io/StackExchange.Redis/Configuration#basic-configuration-strings). For example by adding it to appsettings.json like that:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "AllowedHosts": "*"
}
```

When using with Docker it's easier to use environment variable containg this connection string. It should be called `DOTNET_ConnectionStrings__Redis`. 

# Usage
For examples of HTTP requests, please refer to [http-cases.http](HighScores/test-cases.http). This project has a publicly available hosted node located at https://lb.yogurtthehor.se/. Currently it doesn't have any frontpage, but you for now you can freely use it

All futher examples would be made using publicly available host

## Creating leaderboard
First, you need to create a leaderboard. For that, simply make a GET request or open in browser this link: https://lb.yogurtthehor.se/api/v1/leaderboards/new

It would return JSON object containing id of leaderboard and its secret you can use to upload/modify/delete scores:
```json
{
  "id": 10,
  "secret": "bla-bla-bla"
}
```

Please, store these values, as they cannot be retrived later. Also try not to show secret publicly as it would allow to freely work with your leaderboard. It's understandable that hiding this token still impossible due way of how this project is build. Any simple digging into source code (or even into binaries) or traffic sniffing would allow to get secret key, though it's usually in your best interest to make it as diffficult as possible

## Uploading score
By making simple POST request to https://lb.yogurtthehor.se/api/v1/scores/LB_ID/SECRET/add/NAME/SCORE/TIME you can upload your score. Those are params:

| Name   | Type   | Optional | Description                                        |
|--------|--------|----------|----------------------------------------------------|
| LB_ID  | long   | no       | Id of leaderboard you got when created it          |
| SECRET | string | no       | Secret of leaderboard you got when created it      |
| NAME   | string | no       | Name of user that would appear in leaderboard      |
| SCORE  | long   | no       | Score of user that would appear in score           |
| TIME   | double | yes      | Time it took to make that record. Default is 0     |

## Getting leaderboard
You can see last N scores by making GET request to https://lb.yogurtthehor.se/api/v1/scores/LB_ID/N. For example, you can see this page:
https://lb.yogurtthehor.se/api/v1/scores/9

It would return object with simillar structure:
```json
{
  "scores": [
    {
      "name": "Yogurt",
      "value": 945,
      "time": 0
    },
    {
      "name": "fff",
      "value": 869,
      "time": 0
    }
  ]
}
```

N parameter is optional and can be omited, in that case API would return all recorded results. It's also possible to add query param offset to skip first X results liek that: https://lb.yogurtthehor.se/api/v1/scores/9?offset=1

You can also get score of some specific user with links like this https://lb.yogurtthehor.se/api/v1/scores/9/by/Yogurt. It would response with same object array of "scores" contained previously

## Deleting scores
By making DELETE request to https://lb.yogurtthehor.se/api/v1/scores/LB_ID/SECRET/by/NAME score would be deleted from database. Not only the last one, but all user data

By making DELETE request to https://lb.yogurtthehor.se/api/v1/scores/LB_ID/SECRET all scores would be deleted entirely

## Other methods
You can check [http-cases.http](HighScores/test-cases.http) for some not covered methods. Documentation is still in progress

# Consideration
To use it with WebGL builds CORS are enabled by default for any endpoint and HTTPS is also supported and used as main protocol. 

You should also consider that this API is not produciton ready in any way and made just for my pet project. You should not rely on it as it's very easy to steal secret and put some fake scores or even delete all of that. I may create another super secret string in the future especially for score deletment to make it impossible to use same token for uploading and removal of scores, but that's in TO-DO list

# Unity integration
I am going to make a paid Unity Asset with some very small price that would be a form of support of this project, as it's very easy to write methods to use such APIs, but as for now it's in development 




