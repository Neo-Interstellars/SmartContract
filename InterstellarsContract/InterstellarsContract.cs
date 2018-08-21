using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;
using Helper = Neo.SmartContract.Framework.Helper;


namespace InterstellarsContract
{
    /// <summary>
    /// Whether it ends good or bad ,It was an exprerience!
    /// </summary>
    public class Interstellars : SmartContract
    {

        //合约的拥有者                        
        public static readonly byte[] Owner = "AUvL3noV9g7ebxf8sWYy2x7Yzmb2MNizeB".ToScriptHash();


        //这里包含一个结果
        public delegate void deleOccupyStar(BigInteger result, byte[] sender, BigInteger starId, byte[] name, byte[] mark, BigInteger posX,
              BigInteger posY, BigInteger type, BigInteger totalEnergy, BigInteger mineRatio, BigInteger timestamp, BigInteger costPrice);
        [DisplayName("cccupyStar")]
        public static event deleOccupyStar OccupyStarEvent;

        //挖矿
        public delegate void deleMineStar(BigInteger result, BigInteger times);
        [DisplayName("mineStar")]
        public static event deleMineStar MineStarEvent;

        //改名
        public delegate void deleChangeName(BigInteger result, byte[] name);
        [DisplayName("changeName")]
        public static event deleChangeName ChangeNameEvent;


        //错误码
        public static BigInteger ErrorCodeSuccess = 0;
        public static BigInteger ErrorCodeArgsIncorrect = 1;
        public static BigInteger ErrorCodeCheckWitnessFail = 2;
        public static BigInteger ErrorCodeStarIsExist = 3;
        public static BigInteger ErrorCodeStarIsNotExist = 4;
        public static BigInteger ErrorCodeMineTimesIncorrect = 5;
        public static BigInteger ErrorCodeNotEnoughEnergy = 6;
        public static BigInteger ErrorCodeNotOwner = 7;
        public static BigInteger ErrorCodeNotHaveThisFunc = 8;


        [Serializable]
        public class Star
        {
            public BigInteger Id;
            public byte[] Name;
            public byte[] Mark;
            public BigInteger X;
            public BigInteger Y;
            public BigInteger Type;
            public BigInteger Radius;
            public BigInteger UploadTime;
            public byte[] BCOwner;
            public BigInteger TotalEnergy;
            public BigInteger MineRatio;
            public BigInteger Price;
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
                var callscript = ExecutionEngine.CallingScriptHash;
                if (operation == "getVersion")
                {
                    return Version();
                }
                else if (operation == "getName")
                {
                    return Name();
                }
                else if (operation == "occupyStar")
                {
                    if (args.Length != 11)
                    {
                        Runtime.Log("args is not correct!");
                        return false;
                    }
                    byte[] sender = (byte[])args[0]; //购买星球的人
                    BigInteger starId = (BigInteger)args[1]; //星球的唯一Id
                    byte[] name = (byte[])args[2];
                    byte[] mark = (byte[])args[3];
                    BigInteger posX = (BigInteger)args[4];
                    BigInteger posY = (BigInteger)args[5];
                    BigInteger type = (BigInteger)args[6];
                    BigInteger totalEnergy = (BigInteger)args[7];
                    BigInteger mineRatio = (BigInteger)args[8];
                    BigInteger timestamp = (BigInteger)args[9];
                    BigInteger costPrice = (BigInteger)args[10];

                    return OccupyStar(sender, starId, name, mark, posX, posY, type, totalEnergy, mineRatio, timestamp, costPrice);
                }
                else if (operation == "mine")
                {
                    if (args.Length != 2)
                    {
                        Runtime.Log("args is not correct!");
                        return false;
                    }
                    BigInteger starId = (BigInteger)args[0];
                    BigInteger mineTimes = (BigInteger)args[1];
                    return Mine(starId, mineTimes);
                }
                else if (operation == "changeName")
                {
                    if (args.Length != 3)
                    {
                        Runtime.Log("args is not correct!");
                        return false;
                    }

                    byte[] sender = (byte[])args[0];
                    byte[] starName = (byte[])args[1];
                    BigInteger starId = (BigInteger)args[2];
                    return ChangeName(sender, starName, starId);
                }
                else if (operation == "getStarById")
                {
                    if (args.Length != 1)
                    {
                        Runtime.Log("args is not correct!");
                        return false;
                    }

                    BigInteger starId = (BigInteger)args[0];
                    return GetStarById(starId);
                }
                else if (operation == "delStarById")
                {
                    if (args.Length != 1)
                    {
                        Runtime.Log("args is not correct!");
                        return false;
                    }
                    BigInteger starId = (BigInteger)args[0];
                    return DelStarById(starId);
                }
            }

            return false;
        }

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
            return "1.0.0";
        }


        /// <summary>
        /// 占领星球
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object OccupyStar(byte[] sender, BigInteger starId, byte[] name, byte[] mark, BigInteger posX,
              BigInteger posY, BigInteger type, BigInteger totalEnergy, BigInteger mineRatio, BigInteger timestamp, BigInteger costPrice)
        {
            if (!Runtime.CheckWitness(sender))
            {
                Runtime.Log("check witness fail!");
                OccupyStarEvent(ErrorCodeCheckWitnessFail, sender, starId, name, mark, posX, posY, type, totalEnergy, mineRatio, timestamp, costPrice);
                return false;
            }

            Star star = GetStarInfo(starId);
            if (star != null)
            {
                Runtime.Log("This star is alread exist!");
                OccupyStarEvent(ErrorCodeStarIsExist, sender, starId, name, mark, posX, posY, type, totalEnergy, mineRatio, timestamp, costPrice);
                return false;
            }

            //新建星球
            star = new Star();
            star.Id = starId;
            star.BCOwner = sender;
            star.Mark = mark;
            star.MineRatio = mineRatio;
            star.Name = name;
            star.X = posX;
            star.Y = posY;
            star.TotalEnergy = totalEnergy;
            star.Price = costPrice;
            SaveStarInfo(starId, star);

            OccupyStarEvent(ErrorCodeSuccess, star.BCOwner, star.Id, star.Name, star.Mark, star.X, star.Y, star.Type, star.TotalEnergy, star.MineRatio, star.UploadTime, star.Price);


            return true;// GetErrorCode(ErrorCode.Success);
        }

        /// <summary>
        /// 采矿
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object Mine(BigInteger starId, BigInteger mineTimes)
        {

            Star star = GetStarInfo(starId);
            if (star == null)
            {
                Runtime.Log("star is not exist!");
                MineStarEvent(ErrorCodeStarIsNotExist, mineTimes);
                return false;
            }

            if (mineTimes <= 0 || (mineTimes != 3 && mineTimes != 5 && mineTimes != 8))
            {
                Runtime.Log("mine times is not correct!");
                MineStarEvent(ErrorCodeMineTimesIncorrect, mineTimes);
                return false;
            }

            BigInteger mineEnergy = mineTimes * star.MineRatio;
            if (star.TotalEnergy < mineEnergy)
            {
                Runtime.Log("not have enough energy!");
                MineStarEvent(ErrorCodeNotEnoughEnergy, mineTimes);
                return false;
            }

            star.TotalEnergy -= mineEnergy;
            SaveStarInfo(star.Id, star);


            MineStarEvent(ErrorCodeSuccess, mineTimes);

            return true;
        }

        /// <summary>
        /// 修改自己拥有的star 名字
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object ChangeName(byte[] sender, byte[] starName, BigInteger starId)
        {
            if (!Runtime.CheckWitness(sender))
            {
                Runtime.Log("check witness fail!");
                ChangeNameEvent(ErrorCodeCheckWitnessFail, starName);
                return false;
            }

            Star star = GetStarInfo(starId);
            if (star == null)
            {
                Runtime.Log("star is not exist!");
                ChangeNameEvent(ErrorCodeStarIsNotExist, starName);
                return false;
            }

            if (star.BCOwner != sender)
            {
                Runtime.Log("not owner!");
                ChangeNameEvent(ErrorCodeNotOwner, starName);
                return false;
            }

            star.Name = starName;
            SaveStarInfo(starId, star);
            ChangeNameEvent(ErrorCodeSuccess, starName);
            return true;
        }

        /// <summary>
        /// 获取星球id
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object GetStarById(BigInteger starId)
        {
            Star star = GetStarInfo(starId);
            if (star == null)
            {
                Runtime.Log("star is not exist");
                return null;
            }

            return star;
        }

        /// <summary>
        /// 删除星球通过id，拥有者
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object DelStarById(BigInteger starId)
        {
            if (!Runtime.CheckWitness(Owner))
            {
                Runtime.Log("check witness fail!");
                return false;
            }
            DelStar(starId);

            return true;
        }

        /// <summary>
        /// 保存星球信息
        /// </summary>
        /// <param name="starId"></param>
        /// <param name="star"></param>
        private static void SaveStarInfo(BigInteger starId, Star star)
        {
            byte[] serializedStar = Helper.Serialize(star);
            byte[] realStarId = new byte[] { 0x73, 0x73 }.Concat(starId.AsByteArray());
            Storage.Put(Storage.CurrentContext, realStarId, serializedStar);
        }

        /// <summary>
        /// 获取星球信息
        /// </summary>
        /// <param name="starId"></param>
        /// <returns></returns>
        private static Star GetStarInfo(BigInteger starId)
        {
            byte[] realStarId = new byte[] { 0x73, 0x73 }.Concat(starId.AsByteArray());
            byte[] starData = Storage.Get(Storage.CurrentContext, realStarId);
            if (starData.Length <= 0)
            {
                return null;
            }
            Star star = (Star)Helper.Deserialize(starData);
            return star;
        }


        /// <summary>
        /// 删除星球
        /// </summary>
        /// <param name="starId"></param>
        private static void DelStar(BigInteger starId)
        {
            byte[] realStarId = new byte[] { 0x73, 0x73 }.Concat(starId.AsByteArray());
            Storage.Delete(Storage.CurrentContext, realStarId);
        }


    }
}
