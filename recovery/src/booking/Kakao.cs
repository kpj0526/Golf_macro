namespace booking;

internal class Kakao
{
	public const string API_KEY = "70cdf46ec0274b5f6ec4c04770ea0283";

	public const string TEMPLATE_ID = "64997";

	public const string USER_ID = "481756";

	public const string REDIRECT_URL = "https://www.naver.com/oauth";

	public const string LOGIN_URL = "https://kauth.kakao.com/oauth/authorize?response_type=code&client_id=70cdf46ec0274b5f6ec4c04770ea0283&redirect_uri=https://www.naver.com/oauth";

	public const string HOST_OAUTH_URL = "https://kauth.kakao.com";

	public const string HOST_API_URL = "https://kapi.kakao.com";

	public static string USER_TOKEN;

	public static string ACCESS_TOKEN;

	public static string USER_CODE;
}
