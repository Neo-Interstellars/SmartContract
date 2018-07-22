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

        //合约的拥有者
        public static readonly byte[] Owner = "OOOOOOOOOOOOOOOOOOOOOOOOOO".ToScriptHash();

        //sgas合约的调用地址,缓存索引
        private const string StorageSgasContractAddress = "SgasContractAddress";

        //sgas充值的总量，缓存索引
        private const string StorageSageTotalExcharge = "SgasTotalExcharge";

        //动态调用外部合约方法
        private delegate object DeleDynCall(string method, object[] arr);

        //sgas合约获取txinfo的对外接口名
        private const string SgasMethodGetTxInfo = "getTXInfo";

        [Serializable]
        public class Star
        {
            public BigInteger Id;
            public byte[] Name;
            public byte[] Mark;
            public BigInteger X;
            public BigInteger Y;
            public uint Type;
            public BigInteger Radius;
            public BigInteger Tax;
            public int DisplayIndex;
            public BigInteger Mass;
            public uint UploadTime;
            public byte[] BCOwner;
            public BigInteger TotalEnergy;
            public BigInteger MineRatio;
        }

        //合约主接口
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
                switch (operation) {
                    //合约相关的----------
                    case "getVersion":
                        return Version();
                    case "getName":
                        return Name();
                    case "upgrade":
                        return Upgrade(args);
                    //sgas相关操作------------
                    case "setSgasContract": //设定sgas合约地址
                        return SetSgasContract(args);
                    case "getSgasContract":
                        return GetSgasContract();
                    case "rechargeSgas":
                        return RechargeSgas(args);
                    case "getTotalSgas":
                        return GetTotalSgas();
                    case "querySgas":
                        return QuerySgas(args); //查询在游戏中有多少sgas
                    //游戏相关操作------------
                    case "occupyStar":
                        return OccupyStar(args);
                    case "mine":
                        return Mine(args);
                    case "changeName":
                        return ChangeName(args);
                    case "getStarById":
                        return GetStarById(args);
                    case "delStarById":
                        return DelStarById(args);
                }
            }
            return false;
        }


        #region 合约相关
        /// <summary>
        /// 名字
        /// </summary>
        /// <returns></returns>
        public static string Name()
        {
            return "Interstellars";
        }

        /// <summary>
        /// 版本
        /// </summary>
        /// <returns></returns>
        public static string Version()
        {
            return "0.0.1";
        }

        /// <summary>
        /// 更新合约
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static object Upgrade(params object[] args)
        {
            if (!Runtime.CheckWitness(Owner))
                return false;

            if (args.Length != 1 && args.Length != 9)
                return false;

            byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
            byte[] new_script = (byte[])args[0];
            //如果传入的脚本一样 不继续操作
            if (script == new_script)
                return false;

            byte[] parameter_list = new byte[] { 0x07, 0x10 };
            byte return_type = 0x05;
            bool need_storage = (bool)(object)05;
            string name = "Interstellars";
            string version = "0.1";
            string author = "8272";
            string email = "iss@qq.com";
            string description = "interstellars";

            if (args.Length == 9)
            {
                parameter_list = (byte[])args[1];
                return_type = (byte)args[2];
                need_storage = (bool)args[3];
                name = (string)args[4];
                version = (string)args[5];
                author = (string)args[6];
                email = (string)args[7];
                description = (string)args[8];
            }
            Contract.Migrate(new_script, parameter_list, return_type, need_storage, name, version, author, email, description);
            return true;
        }
        #endregion

        #region sgas相关

        /// <summary>
        /// 获取sgas合约的地址
        /// </summary>
        /// <returns></returns>
        private static object GetSgasContract()
        {
            return Storage.Get(Storage.CurrentContext, StorageSgasContractAddress);
        }

        /// <summary>
        /// 设定sgas合约的地址
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object SetSgasContract(params object[] args)
        {
            if (Runtime.CheckWitness(Owner))
            {
                Storage.Put(Storage.CurrentContext, StorageSgasContractAddress, (byte[])args[0]);
                return new byte[] { 0x01 };
            }
            return new byte[] { 0x00 };
        }

        /// <summary>
        /// 充值sgas
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object RechargeSgas(params object[] args)
        {
            if (args.Length != 2)
            {
                Runtime.Log("args is not correct!");
                return false;
            }

            byte[] owner = (byte[])args[0];
            byte[] txId = (byte[])args[1];
            if (owner.Length != 20) {
                Runtime.Log("recharge owner format is error");
                return false;
            }

            //sgas txid，owner前加0x11
            byte[] keyTxId = new byte[] { 0x11 }.Concat(txId);
            byte[] keyOwner = new byte[] { 0x11 }.Concat(owner);

            byte[] txInfo = Storage.Get(Storage.CurrentContext, keyTxId);
            if (txInfo.Length > 0)
            {
                Runtime.Log("this txid is already hanler!");
                return false;
            }

            object[] queryArgs = new object[1] { txId };
            byte[] sgasHas = (byte[])GetSgasContract();
            DeleDynCall dynCall = (DeleDynCall)sgasHas.ToDelegate();
            object[] callRes = (object[])dynCall(SgasMethodGetTxInfo, queryArgs);

            if (callRes.Length > 0)
            {
                byte[] from = (byte[])queryArgs[0];
                byte[] to = (byte[])queryArgs[1];
                BigInteger value = (BigInteger)queryArgs[2];

                //如果拥有者就是txid的
                if (from == owner)
                {
                    //目标就是执行的这个脚本的hash，地址
                    if (to == ExecutionEngine.ExecutingScriptHash)
                    {
                        //标记这个tx处理过了
                        Storage.Put(Storage.CurrentContext, keyTxId, value);
                        BigInteger money = 0;
                        byte[] ownerMoney = Storage.Get(Storage.CurrentContext, owner);
                        if (ownerMoney.Length > 0)
                        {
                            money = ownerMoney.AsBigInteger();
                        }

                        money += value;

                        //修改合约缓存的sgas数量
                        AddSgas(value);

                        return true;
                    }
                }
            }

            Runtime.Log("call sgas contract fail!");
            return false;
        }

        /// <summary>
        /// 增加本地缓存的sgas数量
        /// </summary>
        /// <param name="value"></param>
        private static void AddSgas(BigInteger value)
        {
            BigInteger totalSgas = Storage.Get(Storage.CurrentContext, StorageSageTotalExcharge).AsBigInteger();
            totalSgas += value;
            Storage.Put(Storage.CurrentContext, StorageSageTotalExcharge, totalSgas);
        }

        /// <summary>
        /// 减少本地缓存的sgas数量
        /// </summary>
        /// <param name="value"></param>
        private static void SubSgas(BigInteger value)
        {
            BigInteger totalSgas = Storage.Get(Storage.CurrentContext, StorageSageTotalExcharge).AsBigInteger();
            totalSgas -= value;
            if (totalSgas > 0)
            {
                Storage.Put(Storage.CurrentContext, StorageSageTotalExcharge, totalSgas);
            }
            else
            {
                Storage.Delete(Storage.CurrentContext, StorageSageTotalExcharge);
            }
        }

        /// <summary>
        /// 获取合约内所有的sgas
        /// </summary>
        /// <returns></returns>
        private static object GetTotalSgas()
        {
            return Storage.Get(Storage.CurrentContext, StorageSageTotalExcharge).AsBigInteger();
        }

        /// <summary>
        /// 查询某个地址的sgas数量
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object QuerySgas(params object[] args)
        {
            if (args.Length != 1)
            {
                Runtime.Log("args is not correct!");
                return false;
            }

            byte[] address = (byte[])args[0];
            var keyAddress = new byte[] { 0x11 }.Concat(address);
            return Storage.Get(Storage.CurrentContext, keyAddress).AsBigInteger();
        }

        #endregion


        #region 游戏逻辑相关
        /// <summary>
        /// 占领星球
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object OccupyStar(params object[] args)
        {
            if (args.Length != 11)
            {
                Runtime.Log("args is not correct!");
                return false;
            }

            byte[] sender = (byte[])args[0]; //购买星球的人
            BigInteger starId = (BigInteger)args[1]; //星球的唯一Id


            if (!Runtime.CheckWitness(sender))
            {
                Runtime.Log("check witness fail!");
                return false;
            }
         
            Star star = GetStarInfo(starId);
            if (star != null)
            {
                Runtime.Log("This star is alread exist!");
                return false;
            }

            //星球的基本信息，暂定这样
            string name = (string)args[2];
            string mark = (string)args[3];
            BigInteger posX = (BigInteger)args[4];
            BigInteger posY = (BigInteger)args[5];
            uint type = (uint)args[6];
            BigInteger totalEnergy = (BigInteger)args[7];
            BigInteger mineRatio = (BigInteger)args[8];
            uint timestamp = (uint)args[9];
            BigInteger costPrice = (BigInteger)args[10];

            //查找这个人的sgas
            BigInteger ownSgas = (BigInteger)QuerySgas(new object[] { sender });
            if (ownSgas < costPrice && costPrice > 0)  //cost price is not right
            {
                Runtime.Log("You have not enough sgas!");
                return false;
            }

            //TODO:这里需要计算燃料费等相关信息，暂时简单减
            ownSgas -= costPrice;
            UpdateAddressSgas(sender, ownSgas);

            //新建星球
            star = new Star();
            star.Id = starId;
            star.BCOwner = sender;
            star.Mark = mark.AsByteArray();
            star.MineRatio = mineRatio;
            star.Name = name.AsByteArray();
            star.X = posX;
            star.Y = posY;
            star.TotalEnergy = totalEnergy;
            SaveStarInfo(starId, star);

            return true;
        }

        /// <summary>
        /// 采矿
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object Mine(params object[] args)
        {
            if (args.Length != 2)
            {
                Runtime.Log("args is not correct!");
                return false;
            }

            BigInteger starId = (BigInteger)args[0];
            uint mineTimes = (uint)args[1];

            Star star = GetStarInfo(starId);
            if (star == null)
            {
                Runtime.Log("star is not exist!");
                return false;
            }

            if (mineTimes <= 0 || (mineTimes != 3 && mineTimes != 5 && mineTimes != 8))
            {
                Runtime.Log("mine times is not correct!");
                return false;
            }

            BigInteger mineEnergy = mineTimes * star.MineRatio;
            if (star.TotalEnergy < mineEnergy)
            {
                Runtime.Log("not have enough energy!");
                return false;
            }

            star.TotalEnergy -= mineEnergy;
            SaveStarInfo(star.Id, star);

            return true;
        }

        /// <summary>
        /// 修改自己拥有的star 名字
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object ChangeName(params object[] args)
        {
            if (args.Length != 3)
            {
                Runtime.Log("args is not correct!");
                return false;
            }

            byte[] sender = (byte[])args[0];
            string starName = (string)args[1];
            BigInteger starId = (BigInteger)args[2];

            if (!Runtime.CheckWitness(sender))
            {
                Runtime.Log("check witness fail!");
                return false;
            }

            Star star = GetStarInfo(starId);
            if (star == null)
            {
                Runtime.Log("star is not exist!");
                return false;
            }

            if (star.BCOwner != sender)
            {
                Runtime.Log("not owner!");
                return false;
            }

            star.Name = starName.AsByteArray();
            SaveStarInfo(starId, star);

            return true;
        }

        /// <summary>
        /// 获取星球id
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object GetStarById(params object[] args)
        {
            if (args.Length != 1)
            {
                Runtime.Log("args is not correct!");
                return false;
            }

            BigInteger starId = (BigInteger)args[0];
            Star star = GetStarInfo(starId);
            if (star == null)
            {
                Runtime.Log("star is not exist");
                return false;
            }

            return star;
        }

        /// <summary>
        /// 删除星球通过id，拥有者
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object DelStarById(params object[] args)
        {

            if (args.Length != 1)
            {
                Runtime.Log("args is not correct!");
                return false;
            }

            if (!Runtime.CheckWitness(Owner))
            {
                Runtime.Log("can`t del by id");
                return false;
            }

            BigInteger starId = (BigInteger)args[0];
            DelStar(starId);

            return true;
        }
        #endregion

        #region 工具
        /// <summary>
        /// 保存星球信息
        /// </summary>
        /// <param name="starId"></param>
        /// <param name="star"></param>
        private static void SaveStarInfo(BigInteger starId, Star star)
        {
            byte[] serializedStar = Helper.Serialize(star);
            byte[] realStarId = new byte[]{ 0x88,0x88}.Concat(starId.AsByteArray());
            Storage.Put(Storage.CurrentContext, realStarId, serializedStar);
        }

        /// <summary>
        /// 获取星球信息
        /// </summary>
        /// <param name="starId"></param>
        /// <returns></returns>
        private static Star GetStarInfo(BigInteger starId)
        {
            byte[] realStarId = new byte[] { 0x88, 0x88 }.Concat(starId.AsByteArray());
            byte[] starData = Storage.Get(Storage.CurrentContext, realStarId);
            if (starData.Length <= 0) {
                return null;
            }
            Star star = (Star)Helper.Deserialize(starData);
            return star;
        }

        /// <summary>
        /// 更新地址的sgas
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private static void UpdateAddressSgas(byte[] sender, BigInteger value)
        {
            byte[] address = new byte[] { 0x11 }.Concat(sender);
            Storage.Put(Storage.CurrentContext, address, value);
        }

        /// <summary>
        /// 删除星球
        /// </summary>
        /// <param name="starId"></param>
        private static void DelStar(BigInteger starId)
        {
            byte[] realStarId = new byte[] { 0x88, 0x88 }.Concat(starId.AsByteArray());
            Storage.Delete(Storage.CurrentContext, realStarId);
        }
        #endregion
    }
}
