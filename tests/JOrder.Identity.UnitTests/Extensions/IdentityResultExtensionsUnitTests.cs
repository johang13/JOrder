using JOrder.Common.Abstractions.Results;
using JOrder.Identity.Extensions;
using Microsoft.AspNetCore.Identity;

namespace JOrder.Identity.UnitTests.Extensions;

public class IdentityResultExtensionsUnitTests
{
    [Fact]
    public void FailedIdentityResult_ToValidationError_ReturnValidationError()
    {
        // Arrange
        var identityResultErrorCode = "InvalidEmail";
        var identityResultDescription = "The email address is invalid.";
        
        var identityResult = IdentityResult.Failed(
                new IdentityError { Code = identityResultErrorCode, Description = identityResultDescription });
        
        // Act
        var result = identityResult.ToValidationError("failure", "fallback message");
        
        // Assert
        Assert.Equal("failure", result.Code);
        Assert.Equal(identityResultDescription, result.Description);
        Assert.Equal(ErrorType.Validation, result.Type);
    }

    [Fact]
    public void FailedIdentityResult_WithMultipleErrors_ToValidationError_JoinsDescriptions()
    {
        // Arrange
        var identityResult = IdentityResult.Failed(
            new IdentityError { Code = "InvalidEmail", Description = "The email address is invalid." },
            new IdentityError { Code = "PasswordTooShort", Description = "Password must be at least 8 characters." });

        // Act
        var result = identityResult.ToValidationError("failure", "fallback message");

        // Assert
        Assert.Equal("failure", result.Code);
        Assert.Equal("The email address is invalid.; Password must be at least 8 characters.", result.Description);
        Assert.Equal(ErrorType.Validation, result.Type);
    }

    [Fact]
    public void FailedIdentityResult_WithOnlyBlankDescriptions_ToValidationError_UsesFallbackMessage()
    {
        // Arrange
        var identityResult = IdentityResult.Failed(
            new IdentityError { Code = "E1", Description = "" },
            new IdentityError { Code = "E2", Description = "   " },
            new IdentityError { Code = "E3", Description = null! });

        // Act
        var result = identityResult.ToValidationError("failure", "fallback message");

        // Assert
        Assert.Equal("failure", result.Code);
        Assert.Equal("fallback message", result.Description);
        Assert.Equal(ErrorType.Validation, result.Type);
    }

    [Fact]
    public void FailedIdentityResult_WithMixedDescriptions_ToValidationError_IgnoresBlankDescriptions()
    {
        // Arrange
        var identityResult = IdentityResult.Failed(
            new IdentityError { Code = "E1", Description = "" },
            new IdentityError { Code = "E2", Description = "Invalid user name." },
            new IdentityError { Code = "E3", Description = "   " },
            new IdentityError { Code = "E4", Description = "Password requires non alphanumeric." });

        // Act
        var result = identityResult.ToValidationError("failure", "fallback message");

        // Assert
        Assert.Equal("failure", result.Code);
        Assert.Equal("Invalid user name.; Password requires non alphanumeric.", result.Description);
        Assert.Equal(ErrorType.Validation, result.Type);
    }
    
    [Fact]
    public void SuccessIdentityResult_ToValidationError_ThrowsInvalidOperationException()
    {
        // Arrange
        var identityResult = IdentityResult.Success;
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => identityResult.ToValidationError("failure", "fallback message"));
    }

    [Fact]
    public void NullIdentityResult_ToValidationError_ThrowsNullArgumentException()
    {
        // Arrange
        IdentityResult identityResult = null!;
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => identityResult.ToValidationError("failure", "fallback message"));
    }
}