# input-processor
Simulates a hard processing task for input data items provided by the user.

![screenshot](/docs/screenshot.png)

## How To Run
1. Clone into a folder say `test`
2. `cd` to `./test`
3. Create a `.env` file

`.env`
```
# Auth
AUTH_USERS=user1:password,user2:password

# Backend
ASPNETCORE_ENVIRONMENT=Production

# WEB
VITE_API_URL=http://localhost/api/ # trailing slash is a must
VITE_APP_TITLE=Input Processor
```

4. Run `docker compose up -d --build`

5. Open the browser and navigate to `http://localhost`. Login using one of the credentials configured in `.env` (AUTH_USERS).

6. Type a string into the input and press `Process`

![gif](/docs/InputProcessor.gif)