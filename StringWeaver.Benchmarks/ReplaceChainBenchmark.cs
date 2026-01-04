using System.Text;

namespace StringWeaver.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class ReplaceChainBenchmark
{
    [Params(1, 5, 10, 25)]
    public int N;

    public string _input = new string('x', 100) + "_a_b_c_d_e_f_g_h_i_j_k_l_m_n_o_p_q_r_s_t_u_v_w_x_y_z_";
    private (string f, string t)[] _pairs;
    [GlobalSetup]
    public void Setup()
    {
        _pairs = Enumerable.Range(0, N).Select(i => ($"_{(char)('a' + i)}_", $"[{i}]")).ToArray();
    }

    [Benchmark(Baseline = true)]
    public string String_Replace()
    {
        var s = _input;
        foreach (var (f, t) in _pairs)
        {
            s = s.Replace(f, t);
        }

        return s;
    }
    [Benchmark]
    public string SW_ReplaceAll()
    {
        var sw = new StringWeaver(_input);
        foreach (var (f, t) in _pairs)
        {
            sw.ReplaceAll(f, t);
        }

        return sw.ToString();
    }
    [Benchmark]
    public string SB_Replace()
    {
        var sb = new StringBuilder(_input);
        foreach (var (f, t) in _pairs)
        {
            sb.Replace(f, t);
        }

        return sb.ToString();
    }
}
