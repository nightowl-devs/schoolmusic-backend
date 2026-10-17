namespace schoolmusic_backend.Services
{
    /// <summary>
    /// Reprezentuje rezultat operacji serwisowej zawierający informację o sukcesie, kodzie statusu HTTP, opcjonalnej
    /// wiadomości oraz ładunku danych
    /// </summary>
    /// <remarks>Udostępnia metody fabryczne tworzące typowe odpowiedzi HTTP (np. 200, 201, 202, 400, 401,
    /// 403, 404, 409, 503).</remarks>
    public class ServiceResult<T>
    {
        public bool Success { get; init; }
        public int StatusCode { get; init; }
        public string? Message { get; init; }
        public T? Data { get; init; }

        public static ServiceResult<T> Ok(T data, string? message = null) 
            => new() { Success = true, StatusCode = 200, Data = data, Message = message };

        public static ServiceResult<T> Created(T data, string? message = null) 
            => new() { Success = true, StatusCode = 201, Data = data, Message = message };

        public static ServiceResult<T> Accepted(T data, string? message = null) 
            => new() { Success = true, StatusCode = 202, Data = data, Message = message };

        public static ServiceResult<T> BadRequest(string message) 
            => new() { Success = false, StatusCode = 400, Message = message };

        public static ServiceResult<T> Unauthorized(string message = "Brak autoryzacji.") 
            => new() { Success = false, StatusCode = 401, Message = message };

        public static ServiceResult<T> Forbidden(string message) 
            => new() { Success = false, StatusCode = 403, Message = message };

        public static ServiceResult<T> NotFound(string message) 
            => new() { Success = false, StatusCode = 404, Message = message };

        public static ServiceResult<T> Conflict(string message) 
            => new() { Success = false, StatusCode = 409, Message = message };

        public static ServiceResult<T> ServiceUnavailable(string message)
            => new() { Success = false, StatusCode = 503, Message = message };
    }
}
