using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Numerics;
using Helper = Neo.SmartContract.Framework.Helper;


/// <summary>
/// 暂定流程,有可能理解问题  
/// 一般占领
/// 1.用户充值sgas 
/// 2.发送sgas到本合约
/// 3.发送交易的txid到合约记录充值金额
/// 3.用户上传星球信息，根据价格扣除金额（这里之后优化)
/// 4.服务端查询上链情况，如果结果有问题修改星球内容，如果没问题交易结束
/// 
/// 改名流程
/// 1.拥有者直接可以改名
/// </summary>
namespace InterstellarsContract
{
    /// <summary>
    /// Whether it ends good or bad ,It was an exprerience!
    /// </summary>
    public class Interstellars : SmartContract
    {

        public static readonly byte[] Owner = "AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y".ToScriptHash();

        private static string Version()
        {
            return "1.00.0dfaf00.1";
        }

        public static object Main(string operation, params object[] args)
        {

            if (Runtime.Trigger == TriggerType.Verification)
            {
                //转账型触发器
                if (Owner.Length == 20)
                {
                    return Runtime.CheckWitness(Owner);
                }
                else if (Owner.Length == 33)
                {
                    byte[] signature = operation.AsByteArray();
                    return VerifySignature(signature, Owner);
                }
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                switch (operation)
                {
                    //合约相关的----------
                    case "getVersion":
                        return Version();
                    case "saveMoney":
                        BigInteger money = (BigInteger)args[0];
                        return SaveMoney(money);
                    case "getMoney":
                        return GetMoney();
                }
            }

            return false;
        }

        private static object SaveMoney(BigInteger money)
        {
           
            byte[] currentMoneyData = Storage.Get(Storage.CurrentContext,"totalMoney");
            BigInteger  totalMoney = new BigInteger(currentMoneyData);
            totalMoney = totalMoney +  money;
            Storage.Put(Storage.CurrentContext, "totalMoney", totalMoney);
            return totalMoney;
        }


        private static object GetMoney()
        {
            byte[] currentMoneyData = Storage.Get(Storage.CurrentContext, "totalMoney");
            BigInteger totalMoney = new BigInteger(currentMoneyData);
            return totalMoney;
        }
    }
}
