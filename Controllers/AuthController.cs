using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TODOLISTAPI.Models;

namespace TODOLISTAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly TodoSmartAlertsContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(TodoSmartAlertsContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid Request"
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.Email == request.Email &&
                    x.Password == request.Password &&
                    x.IsActive == true);

            if (user == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Invalid Email or Password"
                });
            }

            // Generate JWT Token
            var token = GenerateJwtToken(user);

            var response = new LoginResponse
            {
                UserId = user.Id,
                FullName = user.FirstName + " " + user.LastName,
                Email = user.Email,
                Token = token
            };

            return Ok(new
            {
                success = true,
                message = "Login Successful",
                data = response
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            // Check request
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid Request"
                });
            }

            // Required Fields
            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.ConfirmPassword))
            {
                return Ok(new
                {
                    success = false,
                    message = "Please fill all required fields."
                });
            }

            // Password Match
            if (request.Password != request.ConfirmPassword)
            {
                return Ok(new
                {
                    success = false,
                    message = "Passwords do not match."
                });
            }

            // Check Email Exists
            bool emailExists = await _context.Users
                .AnyAsync(x => x.Email == request.Email);

            if (emailExists)
            {
                return Ok(new
                {
                    success = false,
                    message = "Email already exists."
                });
            }

            // Create User
            User user = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = request.Email.Trim(),
                PhoneNumber = request.PhoneNumber,
                Password = request.Password, // Hash in production
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Account created successfully!"
            });
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("UserId", user.Id.ToString()),
                new Claim("FirstName", user.FirstName),
                new Claim("LastName", user.LastName),
                new Claim("FullName", $"{user.FirstName} {user.LastName}")
                // Add Role if your database contains it
                // new Claim(ClaimTypes.Role, user.Role)
            };

            // IMPORTANT: these MUST match the values used for validation in Program.cs
            // (Jwt:Key, Jwt:Issuer, Jwt:Audience in appsettings.json).
            // Previously these were hardcoded here and did not match Program.cs,
            // which caused every token to fail validation right after login
            // ("Session Expired" on the very next request).
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException("JWT Key is missing from appsettings.json");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }



    public class RegisterRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
    }
}


















//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.IdentityModel.Tokens;
//using System.IdentityModel.Tokens.Jwt;
//using System.Security.Claims;
//using System.Text;
//using TODOLISTAPI.Models;

//namespace TODOLISTAPI.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class AuthController : ControllerBase
//    {
//        private readonly TodoSmartAlertsContext _context;

//        public AuthController(TodoSmartAlertsContext context)
//        {
//            _context = context;
//        }

//        [HttpPost("login")]
//        public async Task<IActionResult> Login(LoginRequest request)
//        {
//            if (request == null)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Invalid Request"
//                });
//            }

//            var user = await _context.Users
//                .FirstOrDefaultAsync(x =>
//                    x.Email == request.Email &&
//                    x.Password == request.Password &&
//                    x.IsActive == true);

//            if (user == null)
//            {
//                return Ok(new
//                {
//                    success = false,
//                    message = "Invalid Email or Password"
//                });
//            }

//            // Generate JWT Token
//            var token = GenerateJwtToken(user);

//            var response = new LoginResponse
//            {
//                UserId = user.Id,
//                FullName = user.FirstName + " " + user.LastName,
//                Email = user.Email,
//                Token = token
//            };

//            return Ok(new
//            {
//                success = true,
//                message = "Login Successful",
//                data = response
//            });
//        }

//        [HttpPost("register")]
//        public async Task<IActionResult> Register(RegisterRequest request)
//        {
//            // Check request
//            if (request == null)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Invalid Request"
//                });
//            }

//            // Required Fields
//            if (string.IsNullOrWhiteSpace(request.FirstName) ||
//                string.IsNullOrWhiteSpace(request.LastName) ||
//                string.IsNullOrWhiteSpace(request.Email) ||
//                string.IsNullOrWhiteSpace(request.Password) ||
//                string.IsNullOrWhiteSpace(request.ConfirmPassword))
//            {
//                return Ok(new
//                {
//                    success = false,
//                    message = "Please fill all required fields."
//                });
//            }

//            // Password Match
//            if (request.Password != request.ConfirmPassword)
//            {
//                return Ok(new
//                {
//                    success = false,
//                    message = "Passwords do not match."
//                });
//            }

//            // Check Email Exists
//            bool emailExists = await _context.Users
//                .AnyAsync(x => x.Email == request.Email);

//            if (emailExists)
//            {
//                return Ok(new
//                {
//                    success = false,
//                    message = "Email already exists."
//                });
//            }

//            // Create User
//            User user = new User
//            {
//                FirstName = request.FirstName.Trim(),
//                LastName = request.LastName.Trim(),
//                Email = request.Email.Trim(),
//                PhoneNumber = request.PhoneNumber,
//                Password = request.Password, // Hash in production
//                CreatedAt = DateTime.Now,
//                IsActive = true
//            };

//            _context.Users.Add(user);

//            await _context.SaveChangesAsync();

//            return Ok(new
//            {
//                success = true,
//                message = "Account created successfully!"
//            });
//        }

//        private string GenerateJwtToken(User user)
//        {
//            var claims = new[]
//            {
//        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
//        new Claim(JwtRegisteredClaimNames.Email, user.Email),
//        new Claim("UserId", user.Id.ToString()),
//        new Claim("FirstName", user.FirstName),
//        new Claim("LastName", user.LastName),
//        new Claim("FullName", $"{user.FirstName} {user.LastName}")
//        // Add Role if your database contains it
//        // new Claim(ClaimTypes.Role, user.Role)
//    };

//            var key = new SymmetricSecurityKey(
//                Encoding.UTF8.GetBytes("ThisIsMyVeryStrongSecretKey123456789"));

//            var credentials = new SigningCredentials(
//                key,
//                SecurityAlgorithms.HmacSha256);

//            var token = new JwtSecurityToken(
//                issuer: "TodoSmartAlerts",
//                audience: "TodoSmartAlertsUsers",
//                claims: claims,
//                expires: DateTime.UtcNow.AddDays(30),
//                signingCredentials: credentials
//            );

//            return new JwtSecurityTokenHandler().WriteToken(token);
//        }
//    }



//    public class RegisterRequest
//    {
//        public string FirstName { get; set; }
//        public string LastName { get; set; }
//        public string Email { get; set; }
//        public string PhoneNumber { get; set; }
//        public string Password { get; set; }
//        public string ConfirmPassword { get; set; }
//    }

//    public class LoginRequest
//    {
//        public string Email { get; set; }
//        public string Password { get; set; }
//    }

//    public class LoginResponse
//    {
//        public int UserId { get; set; }
//        public string FullName { get; set; }
//        public string Email { get; set; }
//        public string Token { get; set; }
//    }
//}