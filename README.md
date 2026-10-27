# Campus Gateway: register, sign in and use a JWT

Welcome! This sample gives you a small ASP.NET Core Web API to explore before you build on it for your final assessment. You will register a campus user, sign in and use the returned token to access a protected endpoint.

Start by getting the sample running. Then trace one request through the code, make a small change and test it. Use your assessment brief to decide which features you need to add; this sample is a starting point, not a completed assessment submission.

## What you will practise

By the end of the walkthrough, you should be able to:

- Explain the difference between registering an account and authenticating a request.
- Follow a request from a route into a controller and through to Table Storage.
- Use request models and validation attributes to check incoming JSON.
- Explain why we store password hashes rather than passwords.
- Obtain a JWT and send it in an Authorization header.
- Identify where to add another controller or protected endpoint.

## What is already here?

| Method and route | What it does | Token needed? |
|---|---|---|
| `POST /api/auth/register` | Creates a Student account in the Users table | No |
| `POST /api/auth/login` | Checks credentials and returns a JWT | No |
| `GET /api/auth/me` | Returns the name, email and role from the current token | Yes |

This is the authentication foundation of a gateway. It talks directly to Azure Table Storage and does not yet forward requests to other APIs or Azure Functions. There is no notification delivery, staff provisioning or role management in this version.

## 1. Find your way around

Open the folder containing `CampusGatewayControllers.csproj` in your IDE. Run the commands below from a terminal in that folder.

| File | What to look for |
|---|---|
| `Program.cs` | How services, JWT validation, Swagger and controller routes are connected |
| `Controllers/AuthController.cs` | The three actions that handle HTTP requests |
| `Models/RegisterRequest.cs` | The fields and validation rules for registration |
| `Models/LoginRequest.cs` | The fields and validation rules for login |
| `Models/CampusUser.cs` | The user entity saved in Table Storage |
| `Models/AuthResponses.cs` | The information we choose to return to clients |
| `Services/JwtTokenService.cs` | How the JWT is created and signed |
| `appsettings.json` | Basic logging configuration; secrets are supplied separately |

The code comments are there to guide you. Read a section, predict what it will do, then try it out.

## 2. Prepare your tools

You need:

- The **.NET 10 SDK** and an IDE such as Visual Studio or VS Code.
- Either a running **Azurite Table service** for local storage, or an **Azure Storage account** with Table Storage.
- Docker Desktop if you choose the Docker-based Azurite steps below.

Check your installed SDK:

```powershell
dotnet --list-sdks
```

You should see a `10.0.x` SDK. A runtime alone is not enough to build the project.

The commands in this walkthrough use PowerShell, including Windows PowerShell 5.1.

## 3. Choose your storage

Choose **one** option. If you already have a working Table Storage connection for this sample, keep using it.

### Option A: local storage with Azurite

Start Docker Desktop, then run:

```powershell
docker run -d --name campus-auth-azurite -p 127.0.0.1:10002:10002 -v campus-auth-data:/data mcr.microsoft.com/azure-storage/azurite azurite-table --tableHost 0.0.0.0 --location /data --skipApiVersionCheck
```

This starts the Table service on your computer and keeps its data in a Docker volume. The sample does not need the Blob or Queue services.

If you created this container previously, start it instead of creating it again:

```powershell
docker start campus-auth-azurite
```

Check the service is reachable:

```powershell
Test-NetConnection 127.0.0.1 -Port 10002
```

Look for `TcpTestSucceeded : True`. Then configure the project:

```powershell
dotnet user-secrets set "Storage:ConnectionString" "UseDevelopmentStorage=true"
```

### Option B: an Azure Storage account

Use an account that supports Table Storage. Copy its connection string from your storage account's **Access keys** area in Azure Portal, and run:

```powershell
dotnet user-secrets set "Storage:ConnectionString" "PASTE_YOUR_STORAGE_CONNECTION_STRING"
```

Replace the placeholder with your own value. Keep it private: do not put it into screenshots, source code or a shared repository. The connection must allow the app to create/check the Users table and read/write entities.

**Checkpoint:** Can you explain where the user data will be saved with the option you chose?

## 4. Configure the token signing key

If you already set a JWT key for the earlier sample, you can reuse it. Otherwise, generate a random key and save it locally:

```powershell
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
$jwtKey = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" "$jwtKey"
```

User secrets keep development settings out of the project files. They are not an encrypted production secret store. This project's UserSecretsId is already configured, so you do not need to run `dotnet user-secrets init`.

The signing key lets the API detect whether a token was changed. It does not encrypt the token's contents. Never put a password or storage connection string inside a JWT.

## 5. Build and start the API

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet build
dotnet run --urls "http://localhost:5080"
```

Leave that terminal running. Open [Swagger](http://localhost:5080/swagger) in your browser.

Swagger provides a way to call the API without building a frontend. It is enabled only in Development in this sample. If you launch from your IDE, make sure its environment is Development and use the port shown in its console, or use the terminal command above to keep port 5080.

**Checkpoint:** Find all three endpoints in Swagger. Which one should reject a request without a token?

## 6. Register your first user

In Swagger, expand `POST /api/auth/register`, select **Try it out**, enter the following JSON and select **Execute**:

```json
{
  "email": "student@campus.ac.za",
  "password": "CampusDemo!2026",
  "fullName": "Sam Student"
}
```

Use this sample password only for the demonstration. You should receive **201 Created** with the profile fields. Notice that the response contains neither the password nor its hash.

Try the same request again. You should receive **409 Conflict**, because that email already exists in the Student partition.

Now try a new email with a password shorter than 12 characters. Expect **400 Bad Request** with validation information. Open `RegisterRequest.cs` and find the attribute responsible for the password rule.

If you inspect the Users table using Azure Storage Explorer, the saved entity has this shape:

| Property | Purpose |
|---|---|
| PartitionKey | `Student`, the group used by this sample |
| RowKey | The email address, trimmed and converted to lowercase |
| FullName | The user's display name |
| PasswordHash | A salted hash generated by PasswordHasher |
| CreatedDate | The UTC registration time |
| Timestamp and ETag | Metadata maintained by Table Storage |

The partition and row keys together identify the entity. Registration always creates a Student. The caller cannot choose an Admin or Staff role.

**Pause and explain:** Why do we have both a RegisterRequest model and a CampusUser model instead of accepting a complete CampusUser from the caller?

## 7. Sign in

Call `POST /api/auth/login` with:

```json
{
  "email": "student@campus.ac.za",
  "password": "CampusDemo!2026"
}
```

Expect **200 OK**, an `accessToken` and an `expiresAtUtc` value. Copy the token; you will use it next.

Try the wrong password, and then try an email that has not been registered. Both should return **401 Unauthorized**. This avoids telling the caller which part of the credentials was wrong. Missing or invalid input is handled separately as a 400 validation response.

Open `AuthController.Login`. Follow these steps in the code:

1. Normalize the email and look up the Student entity.
2. Check whether the entity exists.
3. Verify the supplied password against its saved hash.
4. Ask JwtTokenService to create the response token.

**Checkpoint:** Registration saves an account. Login checks credentials and returns a token. Neither step automatically adds that token to all your future requests.

## 8. Use the token

First, clear any existing authorization in Swagger and call `GET /api/auth/me`. Expect **401 Unauthorized**.

Then:

1. Select Swagger's **Authorize** button.
2. Paste the `accessToken` value from login. Paste the token only, without quotes or the word `Bearer`.
3. Select **Authorize**, close the dialog and call `/api/auth/me` again.

You should receive **200 OK**:

```json
{
  "fullName": "Sam Student",
  "email": "student@campus.ac.za",
  "role": "Student"
}
```

Other clients, such as Postman, send the same token in this header:

```text
Authorization: Bearer YOUR_ACCESS_TOKEN
```

The JWT contains Name, Email and Role claims. The API checks its signature, issuer, audience and expiry before allowing access. Tokens last 30 minutes, with 30 seconds of clock tolerance. Sign in again when yours expires.

The Me action reads the claims from the validated token; it does not read the profile from Table Storage again. Changing stored data does not rewrite a token that was already issued.

Swagger may show a security lock on public actions too because of its shared Bearer configuration. The controller's `[AllowAnonymous]` and `[Authorize]` attributes determine the actual access rules.

## 9. Trace how the pieces fit together

| Part | Question to ask while reading |
|---|---|
| Request models | What JSON can a caller send, and what makes it valid? |
| AuthController | Which route runs this action, and which responses can it return? |
| CampusUser | Which values are stored, and how do we find the right entity? |
| JwtTokenService | What goes into the token, and how is it signed? |
| Program.cs | How are these objects created and supplied to the controller? |

The controller uses a **primary constructor**: the parameters after its class name request services from ASP.NET's dependency injection container. The registrations in Program.cs tell ASP.NET what to supply.

`[ApiController]` handles JSON binding and validation. `[AllowAnonymous]` permits registration and login without a token. `[Authorize]` requires authentication for Me. Authentication establishes who the caller is; authorization decides whether they may access a resource. Me currently accepts any valid token for this API, without an additional role restriction.

## 10. Build on this foundation

Use your final assessment brief as the source of requirements. These are suggested next steps, not a mark allocation or a replacement for that brief:

1. **Add a campus feature.** Create a separate NotificationsController with a simple public read endpoint. Give it its own request/response models.
2. **Protect a write action.** Decide which operation needs authentication. Test the same request with and without a token and explain the result.
3. **Connect the feature to your backend.** Use the storage SDK or call a backend function/API, as your brief requires. Handle failure cases and keep credentials outside the code.
4. **Plan privileged roles carefully.** If Staff/Admin access is required, design a trusted way to provision those accounts. Adding a role field to public registration would let callers grant themselves privileges. This sample's login currently looks only in the Student partition, so adding roles also needs a deliberate lookup design.
5. **Document and deploy your work.** Extend your endpoint documentation and follow your brief's hosting requirements. User secrets are for local development; a deployed app needs its own configuration. For example, environment variables use `Storage__ConnectionString` and `Jwt__Key`.

For each change: write down the expected result, implement a small piece, test both a successful and unsuccessful request, and explain what changed. Keep your work in version control without committing secrets. A new controller should not require copying the login or token-generation code into it.

## 11. Check your understanding and behaviour

| Test | Expected result |
|---|---|
| Register a new valid account | 201 |
| Register the same email again, including different letter case | 409 |
| Register with missing fields or a short password | 400 |
| Login with correct credentials | 200 and a JWT |
| Login with an unknown email or wrong password | 401 |
| Call Me without a token or with a malformed token | 401 |
| Call Me with your valid token | 200 and the claims-based profile |

Before extending the project, explain in your own words:

- Why can we verify a password without storing the original password?
- Why should we avoid returning CampusUser directly from the controller?
- Where is a token created, and where is it validated?
- What is the difference between 400, 401 and 409 in this sample?
- Which files would you change to add another protected feature?

If you have used the earlier controller sample on this computer, the same user-secrets ID, storage table layout and endpoint JSON are retained. Existing Student accounts can still be used. Stop the old API first if it is using port 5080.

## Troubleshooting

| Symptom | What to check |
|---|---|
| Connection refused at `127.0.0.1:10002` | Start the Azurite Table service and check its port. `UseDevelopmentStorage=true` expects it on your computer. |
| Docker reports the container name already exists | Use `docker start campus-auth-azurite` rather than creating another container. |
| RandomNumberGenerator has no static GetBytes method | Use the Create()/instance GetBytes() command in step 4; it supports Windows PowerShell 5.1. |
| Storage or JWT configuration is missing | Run user-secrets commands from the folder containing the project file. |
| Registration returns 409 | That email already exists; sign in with it or use a different test email. |
| Me returns 401 after successful login | Paste only the token into Swagger Authorize; check it has not expired and the signing key has not changed. |
| Swagger returns 404 | Check that the API is running in Development and that you used its actual port. |
| The API cannot bind to port 5080 | Stop the previous instance or use another port and update the browser URL. |

## Scope and validation note

This sample focuses on understanding registration and JWT authentication. It does not implement account recovery, email verification, login throttling, MFA, refresh tokens, immediate token revocation or role management. Use HTTPS when exposing authentication beyond local development, and use an established identity provider with OIDC/OAuth for an operational identity system.

This student edition changes documentation and code comments only. A source comparison confirmed that the executable C# content is unchanged from the controller sample. Project XML, settings JSON and the ZIP were checked. Compilation and runtime testing have not been completed in the creation environment; run the build and walkthrough checks on your own setup before extending it.

Further reading: [JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).
