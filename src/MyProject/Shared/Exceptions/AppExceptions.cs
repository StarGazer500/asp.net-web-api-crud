namespace MyProject.Shared.Exceptions;

public abstract class AppException(string message) : Exception(message);
public class NotFoundException(string message) : AppException(message);
public class ConflictException(string message) : AppException(message);
public class StorageException(string message) : AppException(message);
