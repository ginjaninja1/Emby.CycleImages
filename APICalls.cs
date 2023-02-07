using System;
using System.Collections.Generic;
using System.Text;

namespace Emby.CycleImages
{
    class APICalls
    {
        //not ready need more help from dave
        /*
         
        public string BaseApiUrl()
        {
            return string.Format("{0}/emby/", Authentication.EndpointAddress);
        }

        public string UserBasedCall(string request)
        {
            // const url = `${serverAddress}/emby/Users/${userId}/${request}&api_key=${authToken}`;
            var call = BaseApiUrl() + "Users/" + Authentication.CurrentUserId + "/" + request + "api_key=" + Authentication.AuthToken;
            return call;
        }
        public string GetLatestItemsApi(string folderId)
        {
            var request = "Items/Latest?Recursive=true&Limit=20&ParentId=" + folderId + "&EnableImages=true&EnableUserData=true&";
            return string.Format(UserBasedCall(request));

        }
        */
    }
}
