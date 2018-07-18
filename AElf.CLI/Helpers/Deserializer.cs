using System;
using System.Text;
using System.Threading.Tasks;
using AElf.Common.ByteArrayHelpers;
using AElf.Database;
using AElf.Kernel;
using AElf.Kernel.Storages;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json.Linq;


namespace AElf.CLI.Helpers
{
    public class Deserializer
    {
        public static object Deserialize(string sKey, string sValue)
        {
            var byteKey = ByteArrayHelpers.FromHexString(sKey);
            var byteValue = ByteArrayHelpers.FromHexString(sValue);
            
            var key=Key.Parser.ParseFrom(byteKey);
            var keyType = key.Type;                       
            var obj=new JObject();
            
            switch (keyType)
            {
                case TypeName.Bytes:
                    obj=JObject.FromObject(BytesValue.Parser.ParseFrom(byteValue));
                    break;                
                case TypeName.TnBlockHeader:
                    obj=JObject.FromObject(BlockHeader.Parser.ParseFrom(byteValue));                                         
                    break;
                case TypeName.TnBlockBody:
                    obj=JObject.FromObject(BlockBody.Parser.ParseFrom(byteValue));  
                    break;
                case TypeName.TnChain:
                    obj=JObject.FromObject(Chain.Parser.ParseFrom(byteValue));     
                    break;
                case TypeName.TnChange:
                    obj=JObject.FromObject(Change.Parser.ParseFrom(byteValue));   
                    break;                
                case TypeName.TnSmartContractRegistration:
                    obj=JObject.FromObject(SmartContractRegistration.Parser.ParseFrom(byteValue));
                    break;
                case TypeName.TnTransactionResult:
                    obj=JObject.FromObject(TransactionResult.Parser.ParseFrom(byteValue));   
                    break;
                case TypeName.TnTransaction:
                    obj=JObject.FromObject(Transaction.Parser.ParseFrom(byteValue));
                    break;
                case TypeName.TnChangesDict:
                    obj=JObject.FromObject(ChangesDict.Parser.ParseFrom(byteValue));   
                    break;
            }                
            return JObject.FromObject(obj);                                                                  
        }
    }
}