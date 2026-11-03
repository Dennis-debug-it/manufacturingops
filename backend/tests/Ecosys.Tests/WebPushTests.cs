using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecosys.Api.Controllers;
using Ecosys.Infrastructure.Services;

namespace Ecosys.Tests;

public sealed class WebPushTests
{
    // RFC 8291 Appendix A test vector.
    private const string Plaintext = "When I grow up, I want to be a watermelon";
    private const string SenderPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";
    private const string SenderPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string ReceiverPublic = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string AuthSecret = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";
    private const string ExpectedBody = "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

    [Fact]
    public void Encrypt_MatchesRfc8291TestVector()
    {
        using var sender = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.DecodeFromChars(SenderPrivate),
            Q = WebPushCrypto.PointFromUncompressed(Base64Url.DecodeFromChars(SenderPublic)),
        });

        var body = WebPushCrypto.Encrypt(Encoding.UTF8.GetBytes(Plaintext), ReceiverPublic, AuthSecret, Base64Url.DecodeFromChars(Salt), sender);

        Assert.Equal(ExpectedBody, Base64Url.EncodeToString(body));
    }

    [Fact]
    public void VapidAuthorization_IsAVerifiableEs256TokenForTheEndpointOrigin()
    {
        var (publicKey, privateKey) = WebPushCrypto.GenerateVapidKeys();
        var keys = new VapidKeys(publicKey, privateKey, "mailto:ops@example.com");
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

        var header = WebPushCrypto.CreateVapidAuthorization("https://fcm.googleapis.com/fcm/send/abc123", keys, now);

        Assert.StartsWith("vapid t=", header);
        Assert.EndsWith($", k={publicKey}", header);
        var token = header["vapid t=".Length..header.IndexOf(", k=", StringComparison.Ordinal)];
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        Assert.Equal("https://fcm.googleapis.com", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal("mailto:ops@example.com", claims.RootElement.GetProperty("sub").GetString());
        Assert.Equal(now.AddHours(12).ToUnixTimeSeconds(), claims.RootElement.GetProperty("exp").GetInt64());

        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = WebPushCrypto.PointFromUncompressed(Base64Url.DecodeFromChars(publicKey)),
        });
        Assert.True(verifier.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            Base64Url.DecodeFromChars(parts[2]),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://web.push.apple.com/QGx", true)]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]
    [InlineData("https://evilfcm.googleapis.com.attacker.test/x", false)]
    [InlineData("https://attacker.test/fcm.googleapis.com", false)]
    [InlineData("https://169.254.169.254/latest/meta-data", false)]
    [InlineData("https://user@fcm.googleapis.com/x", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedEndpoint_OnlyAcceptsBrowserPushServices(string? endpoint, bool expected)
    {
        Assert.Equal(expected, PushSubscriptionsController.IsAllowedEndpoint(endpoint));
    }
}
