﻿using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Certes.Acme;
using Certes.Acme.Resource;
using Certes.Jws;
using Xunit;

namespace Certes
{
    public class IntegrationTests
    {
        private static Uri stagingServer;

        [Fact]
        public async Task CanRunAccountFlows()
        {
            var dirUri = await GetAvailableStagingServer();

            var ctx = new AcmeContext(dirUri);
            var account = await ctx.NewAccount(
                new[] { $"mailto:certes-{DateTime.UtcNow.Ticks}@example.com" }, true);
            var location = await ctx.GetAccountLocation();

            Assert.NotNull(account);
            Assert.Equal(AccountStatus.Valid, account.Status);

            await ctx.Account.Update(agreeTermsOfService: true);
            await ctx.Account.Update(contact: new[] { $"mailto:certes-{DateTime.UtcNow.Ticks}@example.com" });

            account = await ctx.Account.Deactivate();
            Assert.NotNull(account);
            Assert.Equal(AccountStatus.Deactivated, account.Status);
        }

        [Fact(Skip = "New key is already in use for a different account")]
        public async Task CanChangeAccountKey()
        {
            var dirUri = await GetAvailableStagingServer();

            var ctx = new AcmeContext(dirUri);
            var account = await ctx.NewAccount(
                new[] { $"mailto:certes-{DateTime.UtcNow.Ticks}@example.com" }, true);
            var location = await ctx.GetAccountLocation();

            var newKey = new AccountKey();
            await ctx.ChangeKey(newKey);

            var ctxWithNewKey = new AcmeContext(dirUri, newKey);
            var locationWithNewKey = await ctxWithNewKey.GetAccountLocation();
            Assert.Equal(location, locationWithNewKey);
        }

        /// <summary>
        /// Determines whether this instance [can create new order].
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CanCreateNewOrder()
        {
            var ctx = new AcmeContext(await GetAvailableStagingServer(), Helper.GetAccountKey());
            var orderCtx = await ctx.NewOrder(new[] { "www.es256.certes-ci.certes.com", "mail.es256.certes-ci.certes.com" });
            Assert.IsAssignableFrom<OrderContext>(orderCtx);
            var order = await orderCtx.Resource();
            Assert.NotNull(order);
            Assert.Equal(2, order.Authorizations?.Count);
            Assert.Equal(OrderStatus.Pending, order.Status);

            var authrizations = await orderCtx.Authorizations();

            foreach (var authz in authrizations)
            {
                var httpChallenge = await authz.Http();
                await httpChallenge.Validate();
            }
        }

        private async Task<Uri> GetAvailableStagingServer()
        {
            if (stagingServer != null)
            {
                return stagingServer;
            }

            var servers = new[] {
                new Uri("http://localhost:4001/directory"),
                new Uri("http://boulder-certes-ci.dymetis.com:4001/directory"),
            };

            using (var http = new HttpClient())
            {
                foreach (var uri in servers)
                {
                    try
                    {
                        await http.GetStringAsync(uri);

                        try
                        {
                            var ctx = new AcmeContext(uri, Helper.GetAccountKey());
                            await ctx.NewAccount(new[] { "mailto:fszlin@example.com" }, true);
                        }
                        catch
                        {
                        }

                        return stagingServer = uri;
                    }
                    catch
                    {
                    }
                }
            }

            throw new Exception("No staging server available.");
        }
    }
}
