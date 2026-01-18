using System.Collections.Generic;

namespace Kavkazim.Netcode.Validation
{
    public struct ValidationError
    {
        public string FieldName;
        public string Message;

        public ValidationError(string field, string message)
        {
            FieldName = field;
            Message = message;
        }
    }

    public class LobbyRuntimeContext
    {
        public int CurrentPlayerCount;
        public bool IsTestMode;
    }

    public struct LobbyValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<ValidationError> Errors;

        public LobbyValidationResult(List<ValidationError> errors)
        {
            Errors = errors ?? new List<ValidationError>();
        }

        public static LobbyValidationResult Success()
        {
            return new LobbyValidationResult(new List<ValidationError>());
        }

        public static LobbyValidationResult Failure(string field, string message)
        {
            return new LobbyValidationResult(new List<ValidationError> { new ValidationError(field, message) });
        }
    }
}
