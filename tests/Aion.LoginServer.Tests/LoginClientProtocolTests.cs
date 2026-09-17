using Aion.Bots.Protocol.Login;
using Aion.LoginServer.Network.Crypto;

namespace Aion.LoginServer.Tests;

public sealed class LoginClientProtocolTests
{
	[Fact]
	public void UnscrambleModulus_ReversesServerScramble()
	{
		using var keyPair = LoginRsaKeyPair.Generate();
		var modulus = Assert.IsType<byte[]>(keyPair.PublicParameters.Modulus);

		var scrambled = LoginRsaKeyPair.ScrambleModulus(modulus);

		Assert.Equal(modulus, LoginClientProtocol.UnscrambleModulus(scrambled));
	}
}
