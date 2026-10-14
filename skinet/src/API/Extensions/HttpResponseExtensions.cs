namespace API.Extensions;

public static class HttpResponseExtensions
{
    // The browser may keep the file but must revalidate it on every use, so a deploy is seen at once.
    // Static files carry an ETag, so an unchanged index.html still costs only a 304.
    public static void SetNoCache(this HttpResponse response) =>
        response.Headers.CacheControl = "no-cache";
}
