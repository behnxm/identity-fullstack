using Xunit;

namespace Identity.IntegrationTests.Infrastructure;

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
