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
        /// ��κ������ò����Ƿ�ɹ�
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// ��κ������ò����Ľ����
        /// </summary>
        public int Code { get; private set; }

        /// <summary>
        /// ��κ������ò����������Ϣ
        /// </summary>
        public string Message { get; private set; }


        /// <summary>
        /// ����һ���������κζ����ĳɹ���Ϣ
        /// </summary>
        public static ProgramMessage Success()
        {
            return new ProgramMessage(true, 200, "OK");
        }

        /// <summary>
        /// ����һ���������κζ�����ʧ����Ϣ
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
        /// ��κ������ò����Ƿ�ɹ�
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// ��κ������ò����Ľ����
        /// </summary>
        public int Code { get; private set; }

        /// <summary>
        /// ��κ������ò����������Ϣ
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// ��κ������ò����ķ���ֵ
        /// </summary>
        public T Value { get; private set; }



        /// <summary>
        /// ����һ�����ض����ĳɹ���Ϣ
        /// </summary>
        public static ProgramMessage<T> Success(T val)
        {
            return new ProgramMessage<T>(true, 200, "OK", val);
        }

        /// <summary>
        /// ����һ�����ض�����ʧ����Ϣ
        /// </summary>
        public static ProgramMessage<T> Failure(int code, string msg, T val)
        {
            return new ProgramMessage<T>(false, code, msg, val);
        }
    }
}