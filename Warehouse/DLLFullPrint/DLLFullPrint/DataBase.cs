using System.Data.SqlClient;

namespace DLLFullPrint
{
    class DataBase
    {
        //public static string strConn = "Server=.;Database=Factory;Trusted_Connection=SSPI;Max Pool Size = 512";
        //public SqlConnection connection = new SqlConnection(strConn);
        //public SqlCommand command = new SqlCommand();
        //public SqlDataReader Dr;
        //public SqlDataAdapter sda = new SqlDataAdapter();


        public static string strConn = "Server=(local); Database=Factory;user=sa;password=123456";//(local)。。。。PC-20150915VNKU\\SQLSERVER2005
        public SqlConnection connection = new SqlConnection(strConn);
        public SqlCommand command = new SqlCommand();
        public SqlDataReader Dr;
        public SqlDataAdapter sda = new SqlDataAdapter();



        //public static string strConn = "DSN=warehouse";
        //public OdbcConnection connection = new OdbcConnection(strConn);
        //public OdbcCommand command = new OdbcCommand();
        //public OdbcDataReader Dr;
        //public OdbcDataAdapter sda = new OdbcDataAdapter();
        public DataBase()
        {//在构造函数时将connection打开，不用在调用时再写Open函数
            connection.Open();

        }
        public void Close()
        {
            connection.Close();
            connection.Dispose();
        }
    }
}