using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using LWS_Authentication.Model;
using LWS_Authentication.Repository;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace LWS_Authentication
{
    public class AuthenticationService: AuthenticationRpc.AuthenticationRpcBase
    {
        private readonly ILogger _logger;
        private readonly IAccountRepository _accountRepository;

        public AuthenticationService(ILogger<AuthenticationService> logger, IAccountRepository repository)
        {
            _logger = logger;
            _accountRepository = repository;
        }

        public override async Task<Result> RegisterRequest(RegisterRequestMessage request, ServerCallContext context)
        {
            try
            {
                await _accountRepository.CreateAccountAsync(request);
            }
            catch (Exception e)
            {
                return HandleRegisterError(e, request);
            }

            return new Result
            {
                ResultCode = ResultCode.Success
            };
        }

        public override async Task<Result> LoginRequest(LoginRequestMessage request, ServerCallContext context)
        {
            var loginResult = await _accountRepository.LoginAccountAsync(request);

            if (loginResult == null)
            {
                // Login Failed
                return new Result
                {
                    ResultCode = ResultCode.Forbidden,
                    Message = "Login failed! Please check your email or password."
                };
            }
            
            // Login Succeeds. Create Access Token
            return new Result
            {
                ResultCode = ResultCode.Success,
                Content = JsonConvert.SerializeObject(CreateAccessToken(loginResult))
            };
        }

        public override async Task<Result> AuthenticateUserRequest(AuthenticateUserMessage request, ServerCallContext context)
        {
            var result = await _accountRepository.AuthenticateUserAsync(request.UserToken);

            if (result == null)
            {
                return new Result
                {
                    ResultCode = ResultCode.Forbidden,
                    Message = $"Access Token expired or not-found! Please re-login."
                };
            }

            return new Result
            {
                ResultCode = ResultCode.Success,
                Content = JsonConvert.SerializeObject(result.ToProjection())
            };
        }

        private AccessToken CreateAccessToken(Account account)
        {
            using var shaManaged = new SHA512Managed();
            var targetString = $"{DateTime.Now.Ticks}/{account.UserEmail}/{Guid.NewGuid().ToString()}";
            var targetByte = Encoding.UTF8.GetBytes(targetString);
            var result = shaManaged.ComputeHash(targetByte);

            return new AccessToken
            {
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds(),
                Token = BitConverter.ToString(result).Replace("-", string.Empty)
            };
        }
        
        /// <summary>
        /// Handle Mongo - Write Exception(Global Handler)
        /// </summary>
        /// <param name="superException">Master Exception[Supertype Exception]</param>
        /// <param name="toRegister">User entity tried to register.</param>
        /// <returns>Result Object.</returns>
        [ExcludeFromCodeCoverage]
        private Result HandleRegisterError(Exception superException, RegisterRequestMessage toRegister)
        {
            // When Error type is MongoWriteException
            if (superException is MongoWriteException mongoWriteException)
            {
                // When Error Type is 'Duplicate Key'
                if (mongoWriteException.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    return new Result
                    {
                        ResultCode = ResultCode.Duplicate,
                        Message = $"User Email {toRegister.UserEmail} already exists!"
                    };
                } // Else -> goto Unknown Error.
            }

            // Unknown if exception is not MongoWriteException.
            return new Result
            {
                ResultCode = ResultCode.Unknown,
                Message = $"Unknown Error Occurred! : {superException.Message}"
            };
        }
    }
}