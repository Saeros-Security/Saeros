using Collector.Detection.Mitre;
using FluentAssertions;

namespace Collector.Tests.Mitres;

public class MitreAttackTests
{
     [Theory]
     [InlineData("attack.defense-evasion", "TA0005")]
     [InlineData("attack.t1070.001", "T1070.001")]
     [InlineData("attack.t1105", "T1105")]
     public void MitreAttackResolver_Should_Resolve(string input, string expected)
     {
          var components = MitreAttackResolver.GetComponents([input]).ToList();
          components.Should().HaveCount(1);
          var component = components.Single();
          component.Id.Should().Be(expected);
     } 
     
     [Fact]
     public void MitreAttackResolver_Should_Order()
     {
          var tags = new List<string>
          {
               "attack.credential-access",
               "attack.t1003.001"
          };
          
          var components = MitreAttackResolver.GetComponents(tags).ToList();
          components.Should().HaveCount(2);
          var component = components.First();
          component.SubTechnique.Should().Be("LSASS Memory");
     } 
}