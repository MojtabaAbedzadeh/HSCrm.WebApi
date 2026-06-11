namespace HSCrm.DataModel.Exceptions
{
    public class NotEnoughInventoryException : Exception
    {
        public NotEnoughInventoryException(string message) : base(message)
        {
        }
    }
}