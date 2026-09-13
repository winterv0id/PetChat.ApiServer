namespace PetChat.ApiServer.Model;

public static class LogEvents
{
    #region AUTH
    public const int Registration = 2000;
    public const int Authorization = 2001;
    public const int AuthorizationSuccessful = 2002;
    public const int CreatingJwtToken = 2003;

    public const int AuthorizationUnsuccessful = -2001;
    #endregion

    #region API
    public const int ApiUnknownError = -2100;
    public const int ApiBadRequest = -2101;
    public const int ApiWeirdRequest = -2102;
    #endregion
}