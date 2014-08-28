using System;

namespace Commons
{
	/// <summary>
	/// Description résumée de DataLabelPair.
	/// </summary>
	public class DataLabelPair
	{
		protected object data;
		protected String label;

		public DataLabelPair()
		{
		}

		public DataLabelPair(object data, String label)
		{
			this.data = data;
			this.label = label;
		}

		public object Data
		{
			get { return data; }
			set { data = value; }
		}

		public String Label
		{
			get { return label; }
			set { label = value; }
		}

		public override String ToString()
		{
			return label;
		}
	}
}
