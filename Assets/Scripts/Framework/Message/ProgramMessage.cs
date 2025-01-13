namespace Framework.Message
{
    public struct ProgramMessage
    {
        public ProgramMessage(bool isSuccess, int code, string message)
        {
            IsSuccess = isSuccess;
            Code = code;
            Message = message;
        }

        /// <summary>
        /// 这次函数调用操作是否成功
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 这次函数调用操作的结果码
        /// </summary>
        public int Code { get; private set; }

        /// <summary>
        /// 这次函数调用操作的相关信息
        /// </summary>
        public string Message { get; private set; }


        /// <summary>
        /// 创建一个不返回任何东西的成功消息
        /// </summary>
        public static ProgramMessage Success()
        {
            return new ProgramMessage(true, 200, "OK");
        }

        /// <summary>
        /// 创建一个不返回任何东西的失败消息
        /// </summary>
        public static ProgramMessage Failure(int code, string msg)
        {
            return new ProgramMessage(false, code, msg);
        }

    }

    public struct ProgramMessage<T>
    {
        public ProgramMessage(bool isSuccess, int code, string message, T value = default)
        {
            IsSuccess = isSuccess;
            Code = code;
            Message = message;
            Value = value;
        }

        /// <summary>
        /// 这次函数调用操作是否成功
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 这次函数调用操作的结果码
        /// </summary>
        public int Code { get; private set; }

        /// <summary>
        /// 这次函数调用操作的相关信息
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// 这次函数调用参数的返回值
        /// </summary>
        public T Value { get; private set; }



        /// <summary>
        /// 创建一个返回东西的成功消息
        /// </summary>
        public static ProgramMessage<T> Success(T val)
        {
            return new ProgramMessage<T>(true, 200, "OK", val);
        }

        /// <summary>
        /// 创建一个返回东西的失败消息
        /// </summary>
        public static ProgramMessage<T> Failure(int code, string msg, T val)
        {
            return new ProgramMessage<T>(false, code, msg, val);
        }
    }
}