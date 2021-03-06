//
// Encog(tm) Core v3.2 - .Net Version
// http://www.heatonresearch.com/encog/
//
// Copyright 2008-2013 Heaton Research, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//   
// For more information on Heaton Research copyrights, licenses 
// and trademarks visit:
// http://www.heatonresearch.com/copyright
//
using System;
using System.IO;
using System.Threading.Tasks;

using svm_node = System.Double;

namespace Encog.MathUtil.LIBSVM
{
    // This class was taken from the libsvm package.  We have made some
    // modifications for use in Encog.
    // 
    // http://www.csie.ntu.edu.tw/~cjlin/libsvm/
    //
    // The libsvm Copyright/license is listed here.
    // 
    // Copyright (c) 2000-2010 Chih-Chung Chang and Chih-Jen Lin
    // All rights reserved.
    // 
    // Redistribution and use in source and binary forms, with or without
    // modification, are permitted provided that the following conditions
    // are met:
    // 
    // 1. Redistributions of source code must retain the above copyright
    // notice, this list of conditions and the following disclaimer.
    // 
    // 2. Redistributions in binary form must reproduce the above copyright
    // notice, this list of conditions and the following disclaimer in the
    // documentation and/or other materials provided with the distribution.
    //
    // 3. Neither name of copyright holders nor the names of its contributors
    // may be used to endorse or promote products derived from this software
    // without specific prior written permission.
    // 
    // 
    // THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
    // ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
    // LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
    // A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE REGENTS OR
    // CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
    // EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
    // PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
    // PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
    // LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
    // NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
    // SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.	


    //
    // Kernel Cache
    //
    // l is the number of total data items
    // size is the cache size limit in bytes
    //
    internal class Cache
    {
        //UPGRADE_NOTE: Final was removed from the declaration of 'l '. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'

        //UPGRADE_NOTE: Final was removed from the declaration of 'head '. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'
        private readonly head_t[] head;
        private readonly int l;
        private readonly head_t lru_head;
        private int size;

        internal Cache(int l_, int size_)
        {
            l = l_;
            size = size_;
            head = new head_t[l];
            for (int i = 0; i < l; i++)
                head[i] = new head_t(this);
            size /= 4;
            size -= l*(16/4); // sizeof(head_t) == 16
            lru_head = new head_t(this);
            lru_head.next = lru_head.prev = lru_head;
        }

        private void lru_delete(head_t h)
        {
            // delete from current location
            h.prev.next = h.next;
            h.next.prev = h.prev;
        }

        private void lru_insert(head_t h)
        {
            // insert to last position
            h.next = lru_head;
            h.prev = lru_head.prev;
            h.prev.next = h;
            h.next.prev = h;
        }

        // request data [0,len)
        // return some position p where [p,len) need to be filled
        // (p >= len if nothing needs to be filled)
        // java: simulate pointer using single-element array
        internal virtual int get_data(int index, float[][] data, int len)
        {
            head_t h = head[index];
            if (h.len > 0)
                lru_delete(h);
            int more = len - h.len;

            if (more > 0)
            {
                // free old space
                while (size < more)
                {
                    head_t old = lru_head.next;
                    lru_delete(old);
                    size += old.len;
                    old.data = null;
                    old.len = 0;
                }

                // allocate new space
                var new_data = new float[len];
                if (h.data != null)
                    Array.Copy(h.data, 0, new_data, 0, h.len);
                h.data = new_data;
                size -= more;
                do
                {
                    int _ = h.len;
                    h.len = len;
                    len = _;
                } while (false);
            }

            lru_insert(h);
            data[0] = h.data;
            return len;
        }

        internal virtual void swap_index(int i, int j)
        {
            if (i == j)
                return;

            if (head[i].len > 0)
                lru_delete(head[i]);
            if (head[j].len > 0)
                lru_delete(head[j]);
            do
            {
                float[] _ = head[i].data;
                head[i].data = head[j].data;
                head[j].data = _;
            } while (false);
            do
            {
                int _ = head[i].len;
                head[i].len = head[j].len;
                head[j].len = _;
            } while (false);
            if (head[i].len > 0)
                lru_insert(head[i]);
            if (head[j].len > 0)
                lru_insert(head[j]);

            if (i > j)
                do
                {
                    int _ = i;
                    i = j;
                    j = _;
                } while (false);
            for (head_t h = lru_head.next; h != lru_head; h = h.next)
            {
                if (h.len > i)
                {
                    if (h.len > j)
                        do
                        {
                            float _ = h.data[i];
                            h.data[i] = h.data[j];
                            h.data[j] = _;
                        } while (false);
                    else
                    {
                        // give up
                        lru_delete(h);
                        size += h.len;
                        h.data = null;
                        h.len = 0;
                    }
                }
            }
        }

        #region Nested type: head_t

        private sealed class head_t
        {
            internal float[] data;
            private Cache enclosingInstance;
            internal int len; // data[0,len) is cached in this entry
            internal head_t next; // a cicular list
            internal head_t prev; // a cicular list

            public head_t(Cache enclosingInstance)
            {
                InitBlock(enclosingInstance);
            }

            public Cache Enclosing_Instance
            {
                get { return enclosingInstance; }
            }

            private void InitBlock(Cache enclosingInstance)
            {
                this.enclosingInstance = enclosingInstance;
            }
        }

        #endregion
    }

    //
    // Kernel evaluation
    //
    // the static method k_function is for doing single kernel evaluation
    // the constructor of Kernel prepares to calculate the l*l kernel matrix
    // the member function get_Q is for getting one column from the Q Matrix
    //
    internal abstract class Kernel
    {
        //UPGRADE_NOTE: Final was removed from the declaration of 'degree '. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'
        private readonly double coef0;
        private readonly double degree;
        //UPGRADE_NOTE: Final was removed from the declaration of 'gamma '. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'
        private readonly double gamma;
        private readonly int kernel_type;
        private readonly svm_node[][] x;
        //UPGRADE_NOTE: Final was removed from the declaration of 'x_square '. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'
        private readonly double[] x_square;

        internal Kernel(int l, svm_node[][] x_, svm_parameter param)
        {
            kernel_type = param.kernel_type;
            degree = param.degree;
            gamma = param.gamma;
            coef0 = param.coef0;

            x = (svm_node[][]) x_.Clone();

         
            x_square = null;
        }

        //UPGRADE_NOTE: Final was removed from the declaration of 'coef0 '. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'

        internal abstract float[] get_Q(int column, int len);

        internal virtual void swap_index(int i, int j)
        {
            do
            {
                svm_node[] _ = x[i];
                x[i] = x[j];
                x[j] = _;
            } while (false);
            if (x_square != null)
                do
                {
                    double _ = x_square[i];
                    x_square[i] = x_square[j];
                    x_square[j] = _;
                } while (false);
        }

        private static double tanh(double x)
        {
            double e = Math.Exp(x);
            return 1.0 - 2.0/(e*e + 1);
        }

        internal virtual double kernel_function(int i, int j)
        {
            return dot(x[i], x[j]);

            //switch (kernel_type)
            //{
            //    case svm_parameter.LINEAR:
            //        return dot(x[i], x[j]);

            //    default:
            //        return 0; // java
            //}
        }

        internal static double dot(svm_node[] x, svm_node[] y)
        {
            //DFN-Paul: This is always going to be on compatible dudes
            if (x.Length != y.Length)
                throw new Exception("Dot product between equal sized arrays, please");

            double sum = 0;
            int xlen = x.Length;
            for (int i = 0; i < xlen; i++)
            {
                //sum += x[i].value_Renamed * y[i].value_Renamed;
                //for when this is a double array, THIS CODE SHALL LIVE DAMNIT
                sum += x[i] * y[i];
            }
            return sum;
            //int ylen = y.Length;
            //int i = 0;
            //int j = 0;
            //while (i < xlen && j < ylen)
            //{
            //    if (x[i].index == y[j].index)
            //        sum += x[i++].value_Renamed*y[j++].value_Renamed;
            //    else
            //    {
            //        if (x[i].index > y[j].index)
            //            ++j;
            //        else
            //            ++i;
            //    }
            //}
            //return sum;
        }

        internal static double k_function(svm_node[] x, svm_node[] y, svm_parameter param)
        {
            return dot(x, y);
            //switch (param.kernel_type)
            //{
            //    case svm_parameter.LINEAR:
            //        return dot(x, y);


            //    default:
            //        return 0; // java
            //}
        }
    }

    // Generalized SMO+SVMlight algorithm
    // Solves:
    //
    //	min 0.5(\alpha^T Q \alpha) + b^T \alpha
    //
    //		y^T \alpha = \delta
    //		y_i = +1 or -1
    //		0 <= alpha_i <= Cp for y_i = 1
    //		0 <= alpha_i <= Cn for y_i = -1
    //
    // Given:
    //
    //	Q, b, y, Cp, Cn, and an initial feasible point \alpha
    //	l is the size of vectors and matrices
    //	eps is the stopping criterion
    //
    // solution will be put in \alpha, objective value will be put in obj
    //
    internal class Solver
    {
        internal const sbyte LOWER_BOUND = 0;
        internal const sbyte UPPER_BOUND = 1;
        internal const sbyte FREE = 2;
        internal static readonly double INF = Double.PositiveInfinity;
        internal double Cn;
        internal double Cp;
        internal double[] G; // gradient of objective function
        internal double[] G_bar; // gradient, if we treat free variables as 0
        internal Kernel Q;
        internal int[] active_set;
        internal int active_size;
        internal double[] alpha;
        internal sbyte[] alpha_status; // LOWER_BOUND, UPPER_BOUND, FREE
        internal double[] b;
        internal double eps;
        internal int l;
        internal bool unshrinked; // XXX
        internal sbyte[] y;

        //UPGRADE_NOTE: Final was removed from the declaration of 'INF '. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'

        internal virtual double get_C(int i)
        {
            return (y[i] > 0) ? Cp : Cn;
        }

        internal virtual void update_alpha_status(int i)
        {
            if (alpha[i] >= get_C(i))
                alpha_status[i] = UPPER_BOUND;
            else if (alpha[i] <= 0)
                alpha_status[i] = LOWER_BOUND;
            else
                alpha_status[i] = FREE;
        }

        internal virtual bool is_upper_bound(int i)
        {
            return alpha_status[i] == UPPER_BOUND;
        }

        internal virtual bool is_lower_bound(int i)
        {
            return alpha_status[i] == LOWER_BOUND;
        }

        internal virtual bool is_free(int i)
        {
            return alpha_status[i] == FREE;
        }

        // java: information about solution except alpha,
        // because we cannot return multiple values otherwise...

        internal virtual void swap_index(int i, int j)
        {
            Q.swap_index(i, j);
            do
            {
                sbyte _ = y[i];
                y[i] = y[j];
                y[j] = _;
            } while (false);
            do
            {
                double _ = G[i];
                G[i] = G[j];
                G[j] = _;
            } while (false);
            do
            {
                sbyte _ = alpha_status[i];
                alpha_status[i] = alpha_status[j];
                alpha_status[j] = _;
            } while (false);
            do
            {
                double _ = alpha[i];
                alpha[i] = alpha[j];
                alpha[j] = _;
            } while (false);
            do
            {
                double _ = b[i];
                b[i] = b[j];
                b[j] = _;
            } while (false);
            do
            {
                int _ = active_set[i];
                active_set[i] = active_set[j];
                active_set[j] = _;
            } while (false);
            do
            {
                double _ = G_bar[i];
                G_bar[i] = G_bar[j];
                G_bar[j] = _;
            } while (false);
        }

        internal virtual void reconstruct_gradient()
        {
            // reconstruct inactive elements of G from G_bar and free variables

            if (active_size == l)
                return;

            int i;
            for (i = active_size; i < l; i++)
                G[i] = G_bar[i] + b[i];

            for (i = 0; i < active_size; i++)
                if (is_free(i))
                {
                    float[] Q_i = Q.get_Q(i, l);
                    double alpha_i = alpha[i];
                    for (int j = active_size; j < l; j++)
                        G[j] += alpha_i*Q_i[j];
                }
        }

        internal virtual void Solve(int l, Kernel Q, double[] b_, sbyte[] y_, double[] alpha_, double Cp, double Cn,
                                    double eps, SolutionInfo si, int shrinking)
        {
            this.l = l;
            this.Q = Q;
            b = new double[b_.Length];
            b_.CopyTo(b, 0);
            y = new sbyte[y_.Length];
            y_.CopyTo(y, 0);
            alpha = new double[alpha_.Length];
            alpha_.CopyTo(alpha, 0);
            this.Cp = Cp;
            this.Cn = Cn;
            this.eps = eps;
            unshrinked = false;

            // initialize alpha_status
            {
                alpha_status = new sbyte[l];
                for (int i = 0; i < l; i++)
                    update_alpha_status(i);
            }

            // initialize active set (for shrinking)
            {
                active_set = new int[l];
                for (int i = 0; i < l; i++)
                    active_set[i] = i;
                active_size = l;
            }

            // initialize gradient
            {
                G = new double[l];
                G_bar = new double[l];
                int i;
                for (i = 0; i < l; i++)
                {
                    G[i] = b[i];
                    G_bar[i] = 0;
                }
                for (i = 0; i < l; i++)
                    if (!is_lower_bound(i))
                    {
                        float[] Q_i = Q.get_Q(i, l);
                        double alpha_i = alpha[i];
                        int j;
                        for (j = 0; j < l; j++)
                            G[j] += alpha_i*Q_i[j];
                        if (is_upper_bound(i))
                            for (j = 0; j < l; j++)
                                G_bar[j] += get_C(i)*Q_i[j];
                    }
            }

            // optimization step

            int iter = 0;
            int counter = Math.Min(l, 1000) + 1;
            var working_set = new int[2];

            while (true)
            {
                // max iterations?
                if (iter > 10000)
                    break;
                // show progress and do shrinking

                if (--counter == 0)
                {
                    counter = Math.Min(l, 1000);
                    if (shrinking != 0)
                        do_shrinking();
                    //Console.Error.Write(".");
                }

                if (select_working_set(working_set) != 0)
                {
                    // reconstruct the whole gradient
                    reconstruct_gradient();
                    // reset active set size and check
                    active_size = l;
                    //Console.Error.Write("*");
                    if (select_working_set(working_set) != 0)
                        break;
                    else
                        counter = 1; // do shrinking next iteration
                }

                int i = working_set[0];
                int j = working_set[1];

                ++iter;

                // update alpha[i] and alpha[j], handle bounds carefully

                float[] Q_i = Q.get_Q(i, active_size);
                float[] Q_j = Q.get_Q(j, active_size);

                double C_i = get_C(i);
                double C_j = get_C(j);

                double old_alpha_i = alpha[i];
                double old_alpha_j = alpha[j];

                if (y[i] != y[j])
                {
                    double delta = (- G[i] - G[j])/Math.Max(Q_i[i] + Q_j[j] + 2*Q_i[j], 0);
                    double diff = alpha[i] - alpha[j];
                    alpha[i] += delta;
                    alpha[j] += delta;

                    if (diff > 0)
                    {
                        if (alpha[j] < 0)
                        {
                            alpha[j] = 0;
                            alpha[i] = diff;
                        }
                    }
                    else
                    {
                        if (alpha[i] < 0)
                        {
                            alpha[i] = 0;
                            alpha[j] = - diff;
                        }
                    }
                    if (diff > C_i - C_j)
                    {
                        if (alpha[i] > C_i)
                        {
                            alpha[i] = C_i;
                            alpha[j] = C_i - diff;
                        }
                    }
                    else
                    {
                        if (alpha[j] > C_j)
                        {
                            alpha[j] = C_j;
                            alpha[i] = C_j + diff;
                        }
                    }
                }
                else
                {
                    double delta = (G[i] - G[j])/Math.Max(Q_i[i] + Q_j[j] - 2*Q_i[j], 0);
                    double sum = alpha[i] + alpha[j];
                    alpha[i] -= delta;
                    alpha[j] += delta;
                    if (sum > C_i)
                    {
                        if (alpha[i] > C_i)
                        {
                            alpha[i] = C_i;
                            alpha[j] = sum - C_i;
                        }
                    }
                    else
                    {
                        if (alpha[j] < 0)
                        {
                            alpha[j] = 0;
                            alpha[i] = sum;
                        }
                    }
                    if (sum > C_j)
                    {
                        if (alpha[j] > C_j)
                        {
                            alpha[j] = C_j;
                            alpha[i] = sum - C_j;
                        }
                    }
                    else
                    {
                        if (alpha[i] < 0)
                        {
                            alpha[i] = 0;
                            alpha[j] = sum;
                        }
                    }
                }

                // update G

                double delta_alpha_i = alpha[i] - old_alpha_i;
                double delta_alpha_j = alpha[j] - old_alpha_j;

                for (int k = 0; k < active_size; k++)
                {
                    G[k] += Q_i[k]*delta_alpha_i + Q_j[k]*delta_alpha_j;
                }

                // update alpha_status and G_bar

                {
                    bool ui = is_upper_bound(i);
                    bool uj = is_upper_bound(j);
                    update_alpha_status(i);
                    update_alpha_status(j);
                    int k;
                    if (ui != is_upper_bound(i))
                    {
                        Q_i = Q.get_Q(i, l);
                        if (ui)
                            for (k = 0; k < l; k++)
                                G_bar[k] -= C_i*Q_i[k];
                        else
                            for (k = 0; k < l; k++)
                                G_bar[k] += C_i*Q_i[k];
                    }

                    if (uj != is_upper_bound(j))
                    {
                        Q_j = Q.get_Q(j, l);
                        if (uj)
                            for (k = 0; k < l; k++)
                                G_bar[k] -= C_j*Q_j[k];
                        else
                            for (k = 0; k < l; k++)
                                G_bar[k] += C_j*Q_j[k];
                    }
                }
            }

            // calculate rho

            si.rho = calculate_rho();

            // calculate objective value
            {
                double v = 0;
                int i;
                for (i = 0; i < l; i++)
                    v += alpha[i]*(G[i] + b[i]);

                si.obj = v/2;
            }

            // put back the solution
            {
                for (int i = 0; i < l; i++)
                    alpha_[active_set[i]] = alpha[i];
            }

            si.upper_bound_p = Cp;
            si.upper_bound_n = Cn;

            //Console.Out.Write("\noptimization finished, #iter = " + iter + "\n");
        }

        // return 1 if already optimal, return 0 otherwise
        internal virtual int select_working_set(int[] working_set)
        {
            // return i,j which maximize -grad(f)^T d , under constraint
            // if alpha_i == C, d != +1
            // if alpha_i == 0, d != -1

            double Gmax1 = - INF; // max { -grad(f)_i * d | y_i*d = +1 }
            int Gmax1_idx = - 1;

            double Gmax2 = - INF; // max { -grad(f)_i * d | y_i*d = -1 }
            int Gmax2_idx = - 1;

            for (int i = 0; i < active_size; i++)
            {
                if (y[i] == + 1)
                    // y = +1
                {
                    if (!is_upper_bound(i))
                        // d = +1
                    {
                        if (- G[i] > Gmax1)
                        {
                            Gmax1 = - G[i];
                            Gmax1_idx = i;
                        }
                    }
                    if (!is_lower_bound(i))
                        // d = -1
                    {
                        if (G[i] > Gmax2)
                        {
                            Gmax2 = G[i];
                            Gmax2_idx = i;
                        }
                    }
                }
                    // y = -1
                else
                {
                    if (!is_upper_bound(i))
                        // d = +1
                    {
                        if (- G[i] > Gmax2)
                        {
                            Gmax2 = - G[i];
                            Gmax2_idx = i;
                        }
                    }
                    if (!is_lower_bound(i))
                        // d = -1
                    {
                        if (G[i] > Gmax1)
                        {
                            Gmax1 = G[i];
                            Gmax1_idx = i;
                        }
                    }
                }
            }

            if (Gmax1 + Gmax2 < eps)
                return 1;

            working_set[0] = Gmax1_idx;
            working_set[1] = Gmax2_idx;
            return 0;
        }

        internal virtual void do_shrinking()
        {
            int i, j, k;
            var working_set = new int[2];
            if (select_working_set(working_set) != 0)
                return;
            i = working_set[0];
            j = working_set[1];
            double Gm1 = (- y[j])*G[j];
            double Gm2 = y[i]*G[i];

            // shrink

            for (k = 0; k < active_size; k++)
            {
                if (is_lower_bound(k))
                {
                    if (y[k] == + 1)
                    {
                        if (- G[k] >= Gm1)
                            continue;
                    }
                    else if (- G[k] >= Gm2)
                        continue;
                }
                else if (is_upper_bound(k))
                {
                    if (y[k] == + 1)
                    {
                        if (G[k] >= Gm2)
                            continue;
                    }
                    else if (G[k] >= Gm1)
                        continue;
                }
                else
                    continue;

                --active_size;
                swap_index(k, active_size);
                --k; // look at the newcomer
            }

            // unshrink, check all variables again before final iterations

            if (unshrinked || - (Gm1 + Gm2) > eps*10)
                return;

            unshrinked = true;
            reconstruct_gradient();

            for (k = l - 1; k >= active_size; k--)
            {
                if (is_lower_bound(k))
                {
                    if (y[k] == + 1)
                    {
                        if (- G[k] < Gm1)
                            continue;
                    }
                    else if (- G[k] < Gm2)
                        continue;
                }
                else if (is_upper_bound(k))
                {
                    if (y[k] == + 1)
                    {
                        if (G[k] < Gm2)
                            continue;
                    }
                    else if (G[k] < Gm1)
                        continue;
                }
                else
                    continue;

                swap_index(k, active_size);
                active_size++;
                ++k; // look at the newcomer
            }
        }

        internal virtual double calculate_rho()
        {
            double r;
            int nr_free = 0;
            double ub = INF, lb = - INF, sum_free = 0;
            for (int i = 0; i < active_size; i++)
            {
                double yG = y[i]*G[i];

                if (is_lower_bound(i))
                {
                    if (y[i] > 0)
                        ub = Math.Min(ub, yG);
                    else
                        lb = Math.Max(lb, yG);
                }
                else if (is_upper_bound(i))
                {
                    if (y[i] < 0)
                        ub = Math.Min(ub, yG);
                    else
                        lb = Math.Max(lb, yG);
                }
                else
                {
                    ++nr_free;
                    sum_free += yG;
                }
            }

            if (nr_free > 0)
                r = sum_free/nr_free;
            else
                r = (ub + lb)/2;

            return r;
        }

        #region Nested type: SolutionInfo

        internal class SolutionInfo
        {
            internal double obj;
            internal double r; // for Solver_NU
            internal double rho;
            internal double upper_bound_n;
            internal double upper_bound_p;
        }

        #endregion
    }

    //
    // Q matrices for various formulations
    //
    internal class SVC_Q : Kernel
    {
        //UPGRADE_NOTE: Final was removed from the declaration of 'y '. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'
        //UPGRADE_NOTE: Final was removed from the declaration of 'cache '. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'
        private readonly Cache cache;
        private readonly sbyte[] y;

        internal SVC_Q(svm_problem prob, svm_parameter param, sbyte[] y_) : base(prob.inputCount, prob.inputs, param)
        {
            y = new sbyte[y_.Length];
            y_.CopyTo(y, 0);
            //UPGRADE_WARNING: Data types in Visual C# might be different.  Verify the accuracy of narrowing conversions. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1042_3"'
            cache = new Cache(prob.inputCount, (int) (param.cache_size*(1 << 20)));
        }

        internal override float[] get_Q(int i, int len)
        {
            var data = new float[1][];
            int start;
            if ((start = cache.get_data(i, data, len)) < len)
            {
                for (int j = start; j < len; j++)
                {
                    //UPGRADE_WARNING: Data types in Visual C# might be different.  Verify the accuracy of narrowing conversions. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1042_3"'
                    data[0][j] = (float) (y[i]*y[j]*kernel_function(i, j));
                }
            }
            return data[0];
        }

        internal override void swap_index(int i, int j)
        {
            cache.swap_index(i, j);
            base.swap_index(i, j);
            do
            {
                sbyte _ = y[i];
                y[i] = y[j];
                y[j] = _;
            } while (false);
        }
    }


    public class svm
    {
        //
        // construct and solve various formulations
        //
        private static void solve_c_svc(svm_problem problem, svm_parameter param, double[] alpha, Solver.SolutionInfo si,
                                        double Cp, double Cn)
        {
            //grab number of inputs
            int pInputCount = problem.inputCount;

            //paul-dont know what these are for yet

            //minus ones start as ... minus 1s
            var minus_ones = new double[pInputCount];
            var y = new sbyte[pInputCount];

            int i;

            //loop through our initialization
            for (i = 0; i < pInputCount; i++)
            {
                //zero out alpha
                alpha[i] = 0;

                //initialize to minus 1s
                minus_ones[i] = - 1;

                //y represents what class those inputs belong to (in or out of the class)
                if (problem.truthTable[i] > 0)
                    y[i] = (+ 1);
                else
                    y[i] = - 1;
            }

            var s = new Solver();
            s.Solve(pInputCount, new SVC_Q(problem, param, y), minus_ones, y, alpha, Cp, Cn, param.eps, si, param.shrinking);

            double sum_alpha = 0;
            for (i = 0; i < pInputCount; i++)
                sum_alpha += alpha[i];

            /*if (Cp == Cn)
                Console.Out.Write("nu = " + sum_alpha/(Cp*prob.l) + "\n");*/

            for (i = 0; i < pInputCount; i++)
                alpha[i] *= y[i];
        }
       
      

        //
        // decision_function
        //
        internal class decision_function
        {
            internal double[] alpha;
            internal double rho;
        }


        internal static decision_function svm_train_one(svm_problem prob, svm_parameter param, double Cp, double Cn)
        {
            var alpha = new double[prob.inputCount];
            var si = new Solver.SolutionInfo();
            switch (param.svm_type)
            {
                case svm_parameter.C_SVC:
                    solve_c_svc(prob, param, alpha, si, Cp, Cn);
                    break;              
            }

            //Console.Out.Write("obj = " + si.obj + ", rho = " + si.rho + "\n");

            // output SVs

            int nSV = 0;
            int nBSV = 0;
            for (int i = 0; i < prob.inputCount; i++)
            {
                if (Math.Abs(alpha[i]) > 0)
                {
                    ++nSV;
                    if (prob.truthTable[i] > 0)
                    {
                        if (Math.Abs(alpha[i]) >= si.upper_bound_p)
                            ++nBSV;
                    }
                    else
                    {
                        if (Math.Abs(alpha[i]) >= si.upper_bound_n)
                            ++nBSV;
                    }
                }
            }

            //Console.Out.Write("nSV = " + nSV + ", nBSV = " + nBSV + "\n");

            var f = new decision_function();
            f.alpha = alpha;
            f.rho = si.rho;
            return f;
        }

        // Platt's binary SVM Probablistic Output: an improvement from Lin et al.
        private static void sigmoid_train(int l, double[] dec_values, double[] labels, double[] probAB)
        {
            double A, B;
            double prior1 = 0, prior0 = 0;
            int i;

            for (i = 0; i < l; i++)
                if (labels[i] > 0)
                    prior1 += 1;
                else
                    prior0 += 1;

            int max_iter = 100; // Maximal number of iterations
            double min_step = 1e-10; // Minimal step taken in line search
            double sigma = 1e-3; // For numerically strict PD of Hessian
            double eps = 1e-5;
            double hiTarget = (prior1 + 1.0)/(prior1 + 2.0);
            double loTarget = 1/(prior0 + 2.0);
            var t = new double[l];
            double fApB, p, q, h11, h22, h21, g1, g2, det, dA, dB, gd, stepsize;
            double newA, newB, newf, d1, d2;
            int iter;

            // Initial Point and Initial Fun Value
            A = 0.0;
            B = Math.Log((prior0 + 1.0)/(prior1 + 1.0));
            double fval = 0.0;

            for (i = 0; i < l; i++)
            {
                if (labels[i] > 0)
                    t[i] = hiTarget;
                else
                    t[i] = loTarget;
                fApB = dec_values[i]*A + B;
                if (fApB >= 0)
                    fval += t[i]*fApB + Math.Log(1 + Math.Exp(- fApB));
                else
                    fval += (t[i] - 1)*fApB + Math.Log(1 + Math.Exp(fApB));
            }
            for (iter = 0; iter < max_iter; iter++)
            {
                // Update Gradient and Hessian (use H' = H + sigma I)
                h11 = sigma; // numerically ensures strict PD
                h22 = sigma;
                h21 = 0.0;
                g1 = 0.0;
                g2 = 0.0;
                for (i = 0; i < l; i++)
                {
                    fApB = dec_values[i]*A + B;
                    if (fApB >= 0)
                    {
                        p = Math.Exp(- fApB)/(1.0 + Math.Exp(- fApB));
                        q = 1.0/(1.0 + Math.Exp(- fApB));
                    }
                    else
                    {
                        p = 1.0/(1.0 + Math.Exp(fApB));
                        q = Math.Exp(fApB)/(1.0 + Math.Exp(fApB));
                    }
                    d2 = p*q;
                    h11 += dec_values[i]*dec_values[i]*d2;
                    h22 += d2;
                    h21 += dec_values[i]*d2;
                    d1 = t[i] - p;
                    g1 += dec_values[i]*d1;
                    g2 += d1;
                }

                // Stopping Criteria
                if (Math.Abs(g1) < eps && Math.Abs(g2) < eps)
                    break;

                // Finding Newton direction: -inv(H') * g
                det = h11*h22 - h21*h21;
                dA = (- (h22*g1 - h21*g2))/det;
                dB = (- ((- h21)*g1 + h11*g2))/det;
                gd = g1*dA + g2*dB;


                stepsize = 1; // Line Search
                while (stepsize >= min_step)
                {
                    newA = A + stepsize*dA;
                    newB = B + stepsize*dB;

                    // New function value
                    newf = 0.0;
                    for (i = 0; i < l; i++)
                    {
                        fApB = dec_values[i]*newA + newB;
                        if (fApB >= 0)
                            newf += t[i]*fApB + Math.Log(1 + Math.Exp(- fApB));
                        else
                            newf += (t[i] - 1)*fApB + Math.Log(1 + Math.Exp(fApB));
                    }
                    // Check sufficient decrease
                    if (newf < fval + 0.0001*stepsize*gd)
                    {
                        A = newA;
                        B = newB;
                        fval = newf;
                        break;
                    }
                    else
                        stepsize = stepsize/2.0;
                }

                if (stepsize < min_step)
                {
                    //Console.Error.Write("Line search fails in two-class probability estimates\n");
                    break;
                }
            }

            /*if (iter >= max_iter)
                Console.Error.Write("Reaching maximal iterations in two-class probability estimates\n");*/
            probAB[0] = A;
            probAB[1] = B;
        }

        private static double sigmoid_predict(double decision_value, double A, double B)
        {
            double fApB = decision_value*A + B;
            if (fApB >= 0)
                return Math.Exp(- fApB)/(1.0 + Math.Exp(- fApB));
            else
                return 1.0/(1 + Math.Exp(fApB));
        }

        // Method 2 from the multiclass_prob paper by Wu, Lin, and Weng
        private static void multiclass_probability(int k, double[][] r, double[] p)
        {
            int t;
            int iter = 0, max_iter = 100;
            var Q = new double[k][];
            for (int i = 0; i < k; i++)
            {
                Q[i] = new double[k];
            }
            var Qp = new double[k];
            double pQp, eps = 0.001;

            for (t = 0; t < k; t++)
            {
                p[t] = 1.0/k; // Valid if k = 1
                Q[t][t] = 0;
                for (int j = 0; j < t; j++)
                {
                    Q[t][t] += r[j][t]*r[j][t];
                    Q[t][j] = Q[j][t];
                }
                for (int j = t + 1; j < k; j++)
                {
                    Q[t][t] += r[j][t]*r[j][t];
                    Q[t][j] = (- r[j][t])*r[t][j];
                }
            }
            for (iter = 0; iter < max_iter; iter++)
            {
                // stopping condition, recalculate QP,pQP for numerical accuracy
                pQp = 0;
                for (t = 0; t < k; t++)
                {
                    Qp[t] = 0;
                    for (int j = 0; j < k; j++)
                        Qp[t] += Q[t][j]*p[j];
                    pQp += p[t]*Qp[t];
                }
                double max_error = 0;
                for (t = 0; t < k; t++)
                {
                    double error = Math.Abs(Qp[t] - pQp);
                    if (error > max_error)
                        max_error = error;
                }
                if (max_error < eps)
                    break;

                for (t = 0; t < k; t++)
                {
                    double diff = (- Qp[t] + pQp)/Q[t][t];
                    p[t] += diff;
                    pQp = (pQp + diff*(diff*Q[t][t] + 2*Qp[t]))/(1 + diff)/(1 + diff);
                    for (int j = 0; j < k; j++)
                    {
                        Qp[j] = (Qp[j] + diff*Q[t][j])/(1 + diff);
                        p[j] /= (1 + diff);
                    }
                }
            }
            /*if (iter >= max_iter)
                Console.Error.Write("Exceeds max_iter in multiclass_prob\n");*/
        }

        // Cross-validation decision values for probability estimates
        private static void svm_binary_svc_probability(svm_problem prob, svm_parameter param, double Cp, double Cn,
                                                       double[] probAB)
        {
            int i;
            int nr_fold = 5;
            var perm = new int[prob.inputCount];
            var dec_values = new double[prob.inputCount];

            // random shuffle
            for (i = 0; i < prob.inputCount; i++)
                perm[i] = i;
            for (i = 0; i < prob.inputCount; i++)
            {
                //UPGRADE_WARNING: Data types in Visual C# might be different.  Verify the accuracy of narrowing conversions. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1042_3"'
                int j = i + (int) (SupportClass.Random.NextDouble()*(prob.inputCount - i));
                do
                {
                    int _ = perm[i];
                    perm[i] = perm[j];
                    perm[j] = _;
                } while (false);
            }
            for (i = 0; i < nr_fold; i++)
            {
                int begin = i*prob.inputCount/nr_fold;
                int end = (i + 1)*prob.inputCount/nr_fold;
                int j, k;
                var subprob = new svm_problem();

                subprob.inputCount = prob.inputCount - (end - begin);
                subprob.inputs = new svm_node[subprob.inputCount][];
                subprob.truthTable = new double[subprob.inputCount];

                k = 0;
                for (j = 0; j < begin; j++)
                {
                    subprob.inputs[k] = prob.inputs[perm[j]];
                    subprob.truthTable[k] = prob.truthTable[perm[j]];
                    ++k;
                }
                for (j = end; j < prob.inputCount; j++)
                {
                    subprob.inputs[k] = prob.inputs[perm[j]];
                    subprob.truthTable[k] = prob.truthTable[perm[j]];
                    ++k;
                }
                int p_count = 0, n_count = 0;
                for (j = 0; j < k; j++)
                    if (subprob.truthTable[j] > 0)
                        p_count++;
                    else
                        n_count++;

                if (p_count == 0 && n_count == 0)
                    for (j = begin; j < end; j++)
                        dec_values[perm[j]] = 0;
                else if (p_count > 0 && n_count == 0)
                    for (j = begin; j < end; j++)
                        dec_values[perm[j]] = 1;
                else if (p_count == 0 && n_count > 0)
                    for (j = begin; j < end; j++)
                        dec_values[perm[j]] = - 1;
                else
                {
                    var subparam = (svm_parameter) param.Clone();
                    subparam.probability = 0;
                    subparam.C = 1.0;
                    subparam.nr_weight = 2;
                    subparam.weight_label = new int[2];
                    subparam.weight = new double[2];
                    subparam.weight_label[0] = + 1;
                    subparam.weight_label[1] = - 1;
                    subparam.weight[0] = Cp;
                    subparam.weight[1] = Cn;
                    svm_model submodel = svm_train(subprob, subparam);
                    for (j = begin; j < end; j++)
                    {
                        var dec_value = new double[1];
                        svm_predict_values(submodel, prob.inputs[perm[j]], dec_value);
                        dec_values[perm[j]] = dec_value[0];
                        // ensure +1 -1 order; reason not using CV subroutine
                        dec_values[perm[j]] *= submodel.label[0];
                    }
                }
            }
            sigmoid_train(prob.inputCount, dec_values, prob.truthTable, probAB);
        }

        // Return parameter of a Laplace distribution 
        private static double svm_svr_probability(svm_problem prob, svm_parameter param)
        {
            int i;
            int nr_fold = 5;
            var ymv = new double[prob.inputCount];
            double mae = 0;

            var newparam = (svm_parameter) param.Clone();
            newparam.probability = 0;
            svm_cross_validation(prob, newparam, nr_fold, ymv);
            for (i = 0; i < prob.inputCount; i++)
            {
                ymv[i] = prob.truthTable[i] - ymv[i];
                mae += Math.Abs(ymv[i]);
            }
            mae /= prob.inputCount;
            double std = Math.Sqrt(2*mae*mae);
            int count = 0;
            mae = 0;
            for (i = 0; i < prob.inputCount; i++)
                if (Math.Abs(ymv[i]) > 5*std)
                    count = count + 1;
                else
                    mae += Math.Abs(ymv[i]);
            mae /= (prob.inputCount - count);
            /*Console.Error.Write(
                "Prob. model for test data: target value = predicted value + z,\nz: Laplace distribution e^(-|z|/sigma)/(2sigma),sigma=" +
                mae + "\n");*/
            return mae;
        }

        //
        // Interface functions
        //
        public static svm_model svm_train(svm_problem prob, svm_parameter param)
        {
            var model = new svm_model();
            model.param = param;

          
                // classification
                // find out the number of classes
                int l = prob.inputCount;

                //Paul- max number of allowed classes
                int max_nr_class = 16;
                //Paul- will count up to number of actual classes
                int nr_class = 0;
                var label = new int[max_nr_class];
                var count = new int[max_nr_class];
                var index = new int[l];

                //Paul- This entire loop is simply to count the number of classes we have, and how many of each example we have as well
                //it's effectively a mapping from label to class identifier
                //e.g. if we send in truthTable[7] = {0, 3, 3, 2, 1, 1, 1}
                //then we have label[4] = {0, 3, 2, 1}, and count[4]  = {1, 2, 1, 3}
                int i;
                for (i = 0; i < l; i++)
                {
                    //UPGRADE_WARNING: Data types in Visual C# might be different.  Verify the accuracy of narrowing conversions. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1042_3"'
                    //Paul- Get the label associated with this input
                    //so this input claims that it's digit is 3
                    var this_label = (int) prob.truthTable[i];
                    int j;
                    //Paul- first time through nr_class = 0, so this isn't called
                    for (j = 0; j < nr_class; j++)
                        if (this_label == label[j])
                        {
                            ++count[j];
                            break;
                        }
                    //Paul- index[0] = 0 -- j loop never called
                    //index represents what label index you are -- i.e. what class you represent
                    index[i] = j;

                    //Paul- j == nr_class = 0
                    if (j == nr_class)
                    {
                        //Paul- all this does is to make sure you have enough room if you surpass max number of classes
                        if (nr_class == max_nr_class)
                        {
                            max_nr_class *= 2;
                            var new_data = new int[max_nr_class];
                            Array.Copy(label, 0, new_data, 0, label.Length);
                            label = new_data;

                            new_data = new int[max_nr_class];
                            Array.Copy(count, 0, new_data, 0, count.Length);
                            count = new_data;
                        }
                        label[nr_class] = this_label;
                        count[nr_class] = 1;
                        ++nr_class;
                    }
                }

                // group training data of the same class

                //Paul- Grouping stuff
                //start represents where things should start when grouped together according to label counts
                //e.g. if we send in truthTable[7] = {0, 3, 3, 2, 1, 1, 1}
                //we have 4 classes and 7 examples
                //then we have start[4] = {0, 1, 4, 5}
                //here {y} represents a single input for image class y
                //and x[7][] = { {0}, {1}, {1}, {1}, {2}, {3}, {3}}

                var start = new int[nr_class];
                start[0] = 0;
                for (i = 1; i < nr_class; i++)
                    start[i] = start[i - 1] + count[i - 1];

                //Paul- important - x is a re-ordering of the input data -
                //this code takes all of the samples, then chunks them inside of x according to the digits
                //so 
                var x = new svm_node[l][];

                for (i = 0; i < l; i++)
                {
                    x[start[index[i]]] = prob.inputs[i];
                    ++start[index[i]];
                }

                //Paul- reset start to be correct locations again (we changed it in the above loop)

                start[0] = 0;
                for (i = 1; i < nr_class; i++)
                    start[i] = start[i - 1] + count[i - 1];

                // calculate weighted C

                //Paul- todo - don't know what weighted c represents yet
                var weighted_C = new double[nr_class];
                for (i = 0; i < nr_class; i++)
                    weighted_C[i] = param.C;

                for (i = 0; i < param.nr_weight; i++)
                {
                    int j;
                    for (j = 0; j < nr_class; j++)
                        if (param.weight_label[i] == label[j])
                            break;
                    if (j == nr_class) ;
                    /*Console.Error.Write("warning: class label " + param.weight_label[i] +
                                        " specified in weight is not found\n");*/
                    else
                        weighted_C[j] *= param.weight[i];
                }

                // train k*(k-1)/2 models
                //Paul- As above says, we're going to train a bunch of different models 
                //these are effectively combinining two classes, and training on them
                //so 1-2, 2-3, 1-3 if your digits were {1,2,3}

                var nonzero = new bool[l];
                for (i = 0; i < l; i++)
                    nonzero[i] = false;
                var f = new decision_function[nr_class*(nr_class - 1)/2];

                double[] probA = null, probB = null;
                if (param.probability == 1)
                {
                    probA = new double[nr_class*(nr_class - 1)/2];
                    probB = new double[nr_class*(nr_class - 1)/2];
                }

                int p = 0;
                for (i = 0; i < nr_class; i++)
                    for (int j = i + 1; j < nr_class; j++)
                    {
                        //Paul - here we combine problems from the multiple classes
                        //we take count[class 1] + count[class 2] and combine them

                        var sub_prob = new svm_problem();
                        int si = start[i], sj = start[j];
                        int ci = count[i], cj = count[j];
                        sub_prob.inputCount = ci + cj;
                        sub_prob.inputs = new svm_node[sub_prob.inputCount][];
                        sub_prob.truthTable = new double[sub_prob.inputCount];
                        int k;
                        for (k = 0; k < ci; k++)
                        {
                            sub_prob.inputs[k] = x[si + k];
                            sub_prob.truthTable[k] = + 1;
                        }
                        for (k = 0; k < cj; k++)
                        {
                            sub_prob.inputs[ci + k] = x[sj + k];
                            sub_prob.truthTable[ci + k] = - 1;
                        }

                        if (param.probability == 1)
                        {
                            var probAB = new double[2];
                            svm_binary_svc_probability(sub_prob, param, weighted_C[i], weighted_C[j], probAB);
                            probA[p] = probAB[0];
                            probB[p] = probAB[1];
                        }

                        f[p] = svm_train_one(sub_prob, param, weighted_C[i], weighted_C[j]);
                        for (k = 0; k < ci; k++)
                            if (!nonzero[si + k] && Math.Abs(f[p].alpha[k]) > 0)
                                nonzero[si + k] = true;
                        for (k = 0; k < cj; k++)
                            if (!nonzero[sj + k] && Math.Abs(f[p].alpha[ci + k]) > 0)
                                nonzero[sj + k] = true;
                        ++p;
                    }

                // build output

                model.nr_class = nr_class;

                model.label = new int[nr_class];
                for (i = 0; i < nr_class; i++)
                    model.label[i] = label[i];

                model.rho = new double[nr_class*(nr_class - 1)/2];
                for (i = 0; i < nr_class*(nr_class - 1)/2; i++)
                    model.rho[i] = f[i].rho;

                if (param.probability == 1)
                {
                    model.probA = new double[nr_class*(nr_class - 1)/2];
                    model.probB = new double[nr_class*(nr_class - 1)/2];
                    for (i = 0; i < nr_class*(nr_class - 1)/2; i++)
                    {
                        model.probA[i] = probA[i];
                        model.probB[i] = probB[i];
                    }
                }
                else
                {
                    model.probA = null;
                    model.probB = null;
                }

                int nnz = 0;
                var nz_count = new int[nr_class];
                model.nSV = new int[nr_class];
                for (i = 0; i < nr_class; i++)
                {
                    int nSV = 0;
                    for (int j = 0; j < count[i]; j++)
                        if (nonzero[start[i] + j])
                        {
                            ++nSV;
                            ++nnz;
                        }
                    model.nSV[i] = nSV;
                    nz_count[i] = nSV;
                }

                //Console.Out.Write("Total nSV = " + nnz + "\n");

                model.l = nnz;
                model.SV = new svm_node[nnz][];
                p = 0;
                for (i = 0; i < l; i++)
                    if (nonzero[i])
                        model.SV[p++] = x[i];

                var nz_start = new int[nr_class];
                nz_start[0] = 0;
                for (i = 1; i < nr_class; i++)
                    nz_start[i] = nz_start[i - 1] + nz_count[i - 1];

                model.sv_coef = new double[nr_class - 1][];
                for (i = 0; i < nr_class - 1; i++)
                    model.sv_coef[i] = new double[nnz];

                p = 0;
                for (i = 0; i < nr_class; i++)
                    for (int j = i + 1; j < nr_class; j++)
                    {
                        // classifier (i,j): coefficients with
                        // i are in sv_coef[j-1][nz_start[i]...],
                        // j are in sv_coef[i][nz_start[j]...]

                        int si = start[i];
                        int sj = start[j];
                        int ci = count[i];
                        int cj = count[j];

                        int q = nz_start[i];
                        int k;
                        for (k = 0; k < ci; k++)
                            if (nonzero[si + k])
                                model.sv_coef[j - 1][q++] = f[p].alpha[k];
                        q = nz_start[j];
                        for (k = 0; k < cj; k++)
                            if (nonzero[sj + k])
                                model.sv_coef[i][q++] = f[p].alpha[ci + k];
                        ++p;
                    }
            
            return model;
        }

        public static void svm_cross_validation(svm_problem prob, svm_parameter param, int nr_fold, double[] target)
        {
            int i;
            var perm = new int[prob.inputCount];

            // random shuffle
            for (i = 0; i < prob.inputCount; i++)
                perm[i] = i;
            for (i = 0; i < prob.inputCount; i++)
            {
                //UPGRADE_WARNING: Data types in Visual C# might be different.  Verify the accuracy of narrowing conversions. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1042_3"'
                int j = i + (int) (SupportClass.Random.NextDouble()*(prob.inputCount - i));
                do
                {
                    int _ = perm[i];
                    perm[i] = perm[j];
                    perm[j] = _;
                } while (false);
            }
            for (i = 0; i < nr_fold; i++)
            {
                int begin = i*prob.inputCount/nr_fold;
                int end = (i + 1)*prob.inputCount/nr_fold;
                int j, k;
                var subprob = new svm_problem();

                subprob.inputCount = prob.inputCount - (end - begin);
                subprob.inputs = new svm_node[subprob.inputCount][];
                subprob.truthTable = new double[subprob.inputCount];

                k = 0;
                for (j = 0; j < begin; j++)
                {
                    subprob.inputs[k] = prob.inputs[perm[j]];
                    subprob.truthTable[k] = prob.truthTable[perm[j]];
                    ++k;
                }
                for (j = end; j < prob.inputCount; j++)
                {
                    subprob.inputs[k] = prob.inputs[perm[j]];
                    subprob.truthTable[k] = prob.truthTable[perm[j]];
                    ++k;
                }
                svm_model submodel = svm_train(subprob, param);
                if (param.probability == 1 &&
                    (param.svm_type == svm_parameter.C_SVC))
                {
                    var prob_estimates = new double[svm_get_nr_class(submodel)];
                    for (j = begin; j < end; j++)
                        target[perm[j]] = svm_predict_probability(submodel, prob.inputs[perm[j]], prob_estimates);
                }
                else
                    for (j = begin; j < end; j++)
                        target[perm[j]] = svm_predict(submodel, prob.inputs[perm[j]]);
            }
        }

        public static int svm_get_svm_type(svm_model model)
        {
            return model.param.svm_type;
        }

        public static int svm_get_nr_class(svm_model model)
        {
            return model.nr_class;
        }

        public static void svm_get_labels(svm_model model, int[] label)
        {
            if (model.label != null)
                for (int i = 0; i < model.nr_class; i++)
                    label[i] = model.label[i];
        }

        public static double svm_get_svr_probability(svm_model model)
        {
            //Console.Error.Write("Model doesn't contain information for SVR probability inference\n");
            return 0;
        }

        public static void svm_predict_values(svm_model model, svm_node[] x, double[] dec_values)
        {
           
                int i;
                int nr_class = model.nr_class;
                int l = model.l;

                var kvalue = new double[l];
                for (i = 0; i < l; i++)
                    kvalue[i] = Kernel.k_function(x, model.SV[i], model.param);

                var start = new int[nr_class];
                start[0] = 0;
                for (i = 1; i < nr_class; i++)
                    start[i] = start[i - 1] + model.nSV[i - 1];

                int p = 0;
                int pos = 0;
                for (i = 0; i < nr_class; i++)
                    for (int j = i + 1; j < nr_class; j++)
                    {
                        double sum = 0;
                        int si = start[i];
                        int sj = start[j];
                        int ci = model.nSV[i];
                        int cj = model.nSV[j];

                        int k;
                        double[] coef1 = model.sv_coef[j - 1];
                        double[] coef2 = model.sv_coef[i];
                        for (k = 0; k < ci; k++)
                            sum += coef1[si + k]*kvalue[si + k];
                        for (k = 0; k < cj; k++)
                            sum += coef2[sj + k]*kvalue[sj + k];
                        sum -= model.rho[p++];
                        dec_values[pos++] = sum;
                    }
            
        }

        public static double svm_predict(svm_model model, svm_node[] x)
        {
           
                int i;
                int nr_class = model.nr_class;
                var dec_values = new double[nr_class*(nr_class - 1)/2];
                svm_predict_values(model, x, dec_values);

                var vote = new int[nr_class];
                for (i = 0; i < nr_class; i++)
                    vote[i] = 0;
                int pos = 0;
                for (i = 0; i < nr_class; i++)
                    for (int j = i + 1; j < nr_class; j++)
                    {
                        if (dec_values[pos++] > 0)
                            ++vote[i];
                        else
                            ++vote[j];
                    }

                int vote_max_idx = 0;
                for (i = 1; i < nr_class; i++)
                    if (vote[i] > vote[vote_max_idx])
                        vote_max_idx = i;
                return model.label[vote_max_idx];
            
        }

        public static double svm_predict_probability(svm_model model, svm_node[] x, double[] prob_estimates)
        {
            if ((model.param.svm_type == svm_parameter.C_SVC) &&
                model.probA != null && model.probB != null)
            {
                int i;
                int nr_class = model.nr_class;
                var dec_values = new double[nr_class*(nr_class - 1)/2];
                svm_predict_values(model, x, dec_values);

                double min_prob = 1e-7;
                var tmpArray = new double[nr_class][];
                for (int i2 = 0; i2 < nr_class; i2++)
                {
                    tmpArray[i2] = new double[nr_class];
                }
                double[][] pairwise_prob = tmpArray;

                int k = 0;
                for (i = 0; i < nr_class; i++)
                    for (int j = i + 1; j < nr_class; j++)
                    {
                        pairwise_prob[i][j] =
                            Math.Min(
                                Math.Max(sigmoid_predict(dec_values[k], model.probA[k], model.probB[k]), min_prob),
                                1 - min_prob);
                        pairwise_prob[j][i] = 1 - pairwise_prob[i][j];
                        k++;
                    }
                multiclass_probability(nr_class, pairwise_prob, prob_estimates);

                int prob_max_idx = 0;
                for (i = 1; i < nr_class; i++)
                    if (prob_estimates[i] > prob_estimates[prob_max_idx])
                        prob_max_idx = i;
                return model.label[prob_max_idx];
            }
            else
                return svm_predict(model, x);
        }

     

        public static String svm_check_parameter(svm_problem prob, svm_parameter param)
        {
            // svm_type

            int svm_type = param.svm_type;
            if (svm_type != svm_parameter.C_SVC)
                return "unknown svm type";

            // kernel_type

            int kernel_type = param.kernel_type;
            if (kernel_type != svm_parameter.LINEAR )
                return "unknown kernel type";

            // cache_size,eps,C,nu,p,shrinking

            if (param.cache_size <= 0)
                return "cache_size <= 0";

            if (param.eps <= 0)
                return "eps <= 0";

            if (svm_type == svm_parameter.C_SVC )
                if (param.C <= 0)
                    return "C <= 0";


            if (param.shrinking != 0 && param.shrinking != 1)
                return "shrinking != 0 and shrinking != 1";

            if (param.probability != 0 && param.probability != 1)
                return "probability != 0 and probability != 1";


            return null;
        }

        public static int svm_check_probability_model(svm_model model)
        {
            if (model.param.svm_type == svm_parameter.C_SVC &&
                 model.probA != null && model.probB != null) 
                return 1;
            else
                return 0;
        }

        #region Save/Load [Dead Code]

        //UPGRADE_NOTE: Final was removed from the declaration of 'svm_type_table'. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'
        //internal static readonly String[] svm_type_table = new[]
        //                                                       {"c_svc", "nu_svc", "one_class", "epsilon_svr", "nu_svr"};

        //UPGRADE_NOTE: Final was removed from the declaration of 'kernel_type_table'. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1003_3"'
        //internal static readonly String[] kernel_type_table = new[] {"linear", "polynomial", "rbf", "sigmoid"};

        //public static void svm_save_model(StreamWriter fp, svm_model model)
        //{
        //    svm_parameter param = model.param;

        //    fp.Write("svm_type " + svm_type_table[param.svm_type] + "\n");
        //    fp.Write("kernel_type " + kernel_type_table[param.kernel_type] + "\n");


        //    int nr_class = model.nr_class;
        //    int l = model.l;
        //    fp.Write("nr_class " + nr_class + "\n");
        //    fp.Write("total_sv " + l + "\n");

        //    {
        //        fp.Write("rho");
        //        for (int i = 0; i < nr_class*(nr_class - 1)/2; i++)
        //            fp.Write(" " + model.rho[i]);
        //        fp.Write("\n");
        //    }

        //    if (model.label != null)
        //    {
        //        fp.Write("label");
        //        for (int i = 0; i < nr_class; i++)
        //            fp.Write(" " + model.label[i]);
        //        fp.Write("\n");
        //    }

        //    if (model.probA != null)
        //        // regression has probA only
        //    {
        //        fp.Write("probA");
        //        for (int i = 0; i < nr_class*(nr_class - 1)/2; i++)
        //            fp.Write(" " + model.probA[i]);
        //        fp.Write("\n");
        //    }
        //    if (model.probB != null)
        //    {
        //        fp.Write("probB");
        //        for (int i = 0; i < nr_class*(nr_class - 1)/2; i++)
        //            fp.Write(" " + model.probB[i]);
        //        fp.Write("\n");
        //    }

        //    if (model.nSV != null)
        //    {
        //        fp.Write("nr_sv");
        //        for (int i = 0; i < nr_class; i++)
        //            fp.Write(" " + model.nSV[i]);
        //        fp.Write("\n");
        //    }

        //    fp.Write("SV\n");
        //    double[][] sv_coef = model.sv_coef;
        //    svm_node[][] SV = model.SV;

        //    for (int i = 0; i < l; i++)
        //    {
        //        for (int j = 0; j < nr_class - 1; j++)
        //            fp.Write(sv_coef[j][i] + " ");

        //        svm_node[] p = SV[i];
        //        for (int j = 0; j < p.Length; j++)
        //            fp.Write(j + ":" + p[j].value_Renamed + " ");
        //        fp.Write("\n");
        //    }

        //    fp.Close();
        //}

        //private static double atof(String s)
        //{
        //    return Double.Parse(s);
        //}

        //private static int atoi(String s)
        //{
        //    return Int32.Parse(s);
        //}

        //public static svm_model svm_load_model(StringReader fp)
        //{
        //    // read parameters

        //    var model = new svm_model();
        //    var param = new svm_parameter();
        //    model.param = param;
        //    model.rho = null;
        //    model.probA = null;
        //    model.probB = null;
        //    model.label = null;
        //    model.nSV = null;

        //    while (true)
        //    {
        //        String cmd = fp.ReadLine();
        //        String arg = cmd.Substring(cmd.IndexOf(' ') + 1);

        //        if (cmd.StartsWith("svm_type"))
        //        {
        //            int i;
        //            for (i = 0; i < svm_type_table.Length; i++)
        //            {
        //                if (arg.IndexOf(svm_type_table[i]) != -1)
        //                {
        //                    param.svm_type = i;
        //                    break;
        //                }
        //            }
        //            if (i == svm_type_table.Length)
        //            {
        //                //Console.Error.Write("unknown svm type.\n");
        //                return null;
        //            }
        //        }
        //        else if (cmd.StartsWith("kernel_type"))
        //        {
        //            int i;
        //            for (i = 0; i < kernel_type_table.Length; i++)
        //            {
        //                if (arg.IndexOf(kernel_type_table[i]) != -1)
        //                {
        //                    param.kernel_type = i;
        //                    break;
        //                }
        //            }
        //            if (i == kernel_type_table.Length)
        //            {
        //                //Console.Error.Write("unknown kernel function.\n");
        //                return null;
        //            }
        //        }
        //        else if (cmd.StartsWith("degree"))
        //            param.degree = atof(arg);
        //        else if (cmd.StartsWith("gamma"))
        //            param.gamma = atof(arg);
        //        else if (cmd.StartsWith("coef0"))
        //            param.coef0 = atof(arg);
        //        else if (cmd.StartsWith("nr_class"))
        //            model.nr_class = atoi(arg);
        //        else if (cmd.StartsWith("total_sv"))
        //            model.l = atoi(arg);
        //        else if (cmd.StartsWith("rho"))
        //        {
        //            int n = model.nr_class*(model.nr_class - 1)/2;
        //            model.rho = new double[n];
        //            var st = new SupportClass.Tokenizer(arg);
        //            for (int i = 0; i < n; i++)
        //                model.rho[i] = atof(st.NextToken());
        //        }
        //        else if (cmd.StartsWith("label"))
        //        {
        //            int n = model.nr_class;
        //            model.label = new int[n];
        //            var st = new SupportClass.Tokenizer(arg);
        //            for (int i = 0; i < n; i++)
        //                model.label[i] = atoi(st.NextToken());
        //        }
        //        else if (cmd.StartsWith("probA"))
        //        {
        //            int n = model.nr_class*(model.nr_class - 1)/2;
        //            model.probA = new double[n];
        //            var st = new SupportClass.Tokenizer(arg);
        //            for (int i = 0; i < n; i++)
        //                model.probA[i] = atof(st.NextToken());
        //        }
        //        else if (cmd.StartsWith("probB"))
        //        {
        //            int n = model.nr_class*(model.nr_class - 1)/2;
        //            model.probB = new double[n];
        //            var st = new SupportClass.Tokenizer(arg);
        //            for (int i = 0; i < n; i++)
        //                model.probB[i] = atof(st.NextToken());
        //        }
        //        else if (cmd.StartsWith("nr_sv"))
        //        {
        //            int n = model.nr_class;
        //            model.nSV = new int[n];
        //            var st = new SupportClass.Tokenizer(arg);
        //            for (int i = 0; i < n; i++)
        //                model.nSV[i] = atoi(st.NextToken());
        //        }
        //        else if (cmd.StartsWith("SV"))
        //        {
        //            break;
        //        }
        //        else
        //        {
        //            //Console.Error.Write("unknown text in model file\n");
        //            return null;
        //        }
        //    }

        //    // read sv_coef and SV

        //    int m = model.nr_class - 1;
        //    int l = model.l;
        //    model.sv_coef = new double[m][];
        //    for (int i = 0; i < m; i++)
        //    {
        //        model.sv_coef[i] = new double[l];
        //    }
        //    model.SV = new svm_node[l][];

        //    for (int i = 0; i < l; i++)
        //    {
        //        String line = fp.ReadLine();
        //        var st = new SupportClass.Tokenizer(line, " \t\n\r\f:");

        //        for (int k = 0; k < m; k++)
        //            model.sv_coef[k][i] = atof(st.NextToken());
        //        int n = st.Count/2;
        //        model.SV[i] = new svm_node[n];
        //        for (int j = 0; j < n; j++)
        //        {
        //            model.SV[i][j] = new svm_node();
        //            //model.SV[i][j].index = atoi(st.NextToken());
        //            model.SV[i][j].value_Renamed = atof(st.NextToken());
        //        }
        //    }

        //    return model;
        //}

        #endregion
    }
}
