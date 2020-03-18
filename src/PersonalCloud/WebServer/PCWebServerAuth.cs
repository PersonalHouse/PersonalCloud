using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Authentication;
using System.Linq;
using System.Globalization;

namespace NSPersonalCloud.FileSharing
{
    class PCWebServerAuth : AuthenticationModuleBase
    {
        readonly IPCService pCService;
        public PCWebServerAuth(string baseRoute, IPCService pcsrv)
            : base(baseRoute)
        {
            pCService = pcsrv;
        }
        protected override Task<IPrincipal> AuthenticateAsync(IHttpContext context)
        {
            try
            {
                var authverstr = context.Request.Headers[AuthDefinitions.AuthenticationVersion].Trim();
                if (string.IsNullOrWhiteSpace(authverstr))
                {
                    return Task.FromResult(Auth.NoUser);
                }
                var authver = int.Parse(authverstr, CultureInfo.InvariantCulture);
                if (authver< AuthDefinitions.CurAuthVersion)
                {
                    return Task.FromResult(Auth.NoUser);
                }

                var tsstr = context.Request.Headers[AuthDefinitions.AuthenticationTimeStamp].Trim();
                if (string.IsNullOrWhiteSpace(tsstr))
                {
                    return Task.FromResult(Auth.NoUser);
                }
                var ts = long.Parse(tsstr, CultureInfo.InvariantCulture);


                var hashstr = context.Request.Headers[AuthDefinitions.AuthenticationHash].Trim();
                if (string.IsNullOrWhiteSpace(hashstr))
                {
                    return Task.FromResult(Auth.NoUser);
                }
                var hash = ulong.Parse(hashstr, CultureInfo.InvariantCulture);


                var pcid = context.Request.Headers[AuthDefinitions.AuthenticationPCId].Trim();
                if (string.IsNullOrWhiteSpace(hashstr))
                {
                    return Task.FromResult(Auth.NoUser);
                }
                var key = pCService.PersonalClouds.FirstOrDefault(x => x.Id == pcid)?.MasterKey;
                if (key==null)
                {
                    return Task.FromResult(Auth.NoUser);
                }

                var url = context.Request.Url.ToString().ToUpperInvariant();

                //if (authver == 1)
                {
                    if (hash == EmbedIOAuthentication.V1Auth(ts, url, key))
                    {
                        var userName = "pc";
                        var roles = new List<string>();
                        return Task.FromResult((IPrincipal)new GenericPrincipal(new GenericIdentity(userName, AuthDefinitions.AuthenticationType), roles.ToArray()));
                    }
                    return Task.FromResult(Auth.NoUser);
                }

                //return Task.FromResult(Auth.CreateUnauthenticatedPrincipal(AuthDefinitions.AuthenticationType));


            }
            catch
            {
                return Task.FromResult(Auth.NoUser);
            }

        }
    }
}
