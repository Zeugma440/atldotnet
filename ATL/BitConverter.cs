/* Author:
 *  - Nathan Baulch (nbaulch@bigpond.net.au
 * 
 * References:
 *  - http://cch.loria.fr/documentation/IEEE754/numerical_comp_guide/ncg_math.doc.html
 *  - http://groups.google.com/groups?selm=MPG.19a6985d4683f5d398a313%40news.microsoft.com
 */

using System;

namespace Nato.LongDouble
{
	public class BitConverter
	{
		//converts the next 10 bytes of Value starting at StartIndex into a double
		public static double ToDouble(byte[] Value,int StartIndex)
		{
			if(Value == null)
				throw new ArgumentNullException("Value");

			if(Value.Length < StartIndex + 10)
				throw new ArgumentException("Combination of Value length and StartIndex was not large enough.");

			//extract fields
			byte s = (byte)(Value[9] & 0x80);
			short e = (short)(((Value[9] & 0x7F) << 8) | Value[8]);
			byte j = (byte)(Value[7] & 0x80);
			long f = Value[7] & 0x7F;
			for(sbyte i = 6; i >= 0; i--)
			{
				f <<= 8;
				f |= Value[i];
			}

			if(e == 0) //subnormal, pseudo-denormal or zero
				return 0;

			if(j == 0)
				throw new NotSupportedException();

			if(e == 0x7FFF) //+infinity, -infinity or nan
			{
				if(f != 0)
					return double.NaN;
				if(s == 0)
					return double.PositiveInfinity;
				else
					return double.NegativeInfinity;
			}

			//translate f
			f >>= 11;

			//translate e
			e -= (0x3FFF - 0x3FF);

			if(e >= 0x7FF) //outside the range of a double
				throw new OverflowException();
			else if(e < -51) //too small to translate into subnormal
				return 0;
			else if(e < 0) //too small for normal but big enough to represent as subnormal
			{
				f |= 0x10000000000000;
				f >>= (1 - e);
				e = 0;
			}

			byte[] newBytes = System.BitConverter.GetBytes(f);

			newBytes[7] = (byte)(s | (e >> 4));
			newBytes[6] = (byte)(((e & 0x0F) << 4) | newBytes[6]);

			return System.BitConverter.ToDouble(newBytes,0);
		}

		//converts Value into a long double byte array of length 10
		public static byte[] GetBytes(double Value)
		{
			byte[] oldBytes = System.BitConverter.GetBytes(Value);

			//extract fields
			byte s = (byte)(oldBytes[7] & 0x80);
			short e = (short)(((oldBytes[7] & 0x7F) << 4) | ((oldBytes[6] & 0xF0) >> 4));
			byte j = 0x80;
			long f = oldBytes[6] & 0xF;
			for(sbyte i = 5; i >= 0; i--)
			{
				f <<= 8;
				f |= oldBytes[i];
			}

			//translate f
			f <<= 11;

			if(e == 0x7FF) //+infinity, -infinity or nan
				e = 0x7FFF;
			else if(e == 0 && f == 0) //zero
				j = 0;
			else //normal or subnormal
			{
				if(e == 0) //subnormal
				{
					f <<= 1;
					while(f > 0)
					{
						e--;
						f <<= 1;
					}
					f &= long.MaxValue;
				}

				e += (0x3FFF - 0x3FF); //translate e
			}

			byte[] newBytes = new byte[10];
			System.BitConverter.GetBytes(f).CopyTo(newBytes,0);

			newBytes[9] = (byte)(s | (e >> 8));
			newBytes[8] = (byte)(e & 0xFF);
			newBytes[7] = (byte)(j | newBytes[7]);

			return newBytes;
		}
	}
}