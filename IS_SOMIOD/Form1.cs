using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
// Adicionar estas referências:
using RestSharp;

namespace IS_SOMIOD
{
    public partial class Form1 : Form
    {
        // --- CONFIGURAÇÕES DA API SOMIOD ---
        private const string API_BASE = "http://localhost:62056/api/somiod";
        private const string APP_NAME = "smart-parking";

        // Variável para guardar qual o botão (Vaga X) que está à espera de sair
        private Button slotParaSair = null;

        public Form1()
        {
            InitializeComponent();

            // Ligar os eventos manualmente
            this.btnMais1.Click += new EventHandler(this.btnMais1_Click);
            this.btnMenos1.Click += new EventHandler(this.btnMenos1_Click);

            InitializeDefaultColors();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // [API] Criar a estrutura base (Application e Containers) ao iniciar
            await CriarRecursosIniciais();
        }

        private void InitializeDefaultColors()
        {
            var allSlots = new[] { btn1, btn2, btn3, btn4, btn5, btn6, btn7, btn8 };
            foreach (var b in allSlots)
            {
                b.UseVisualStyleBackColor = false;
                b.BackColor = Color.LightGreen;
            }

            // Semáforo inicia livre (Verde claro na sua lógica inicial)
            btnSemaforo.BackColor = Color.LightGreen;

            btnMenos1.UseVisualStyleBackColor = true;
            btnMenos1.BackColor = SystemColors.Control;

            slotParaSair = null;
        }

        // --- LÓGICA DE BOOTSTRAP API ---
        // --- LÓGICA DE BOOTSTRAP (CORRIGIDA) ---
        private async Task CriarRecursosIniciais()
        {
            var client = new RestClient(API_BASE);

            // 1. Tentar criar a Aplicação
            var reqApp = new RestRequest("", Method.Post);
            reqApp.AddJsonBody(new { res_type = "application", resource_name = APP_NAME });

            var respApp = await client.ExecuteAsync(reqApp);

            // Se falhou e não foi por conflito (já existe), mostramos erro na consola
            if (!respApp.IsSuccessful)
            {
                System.Diagnostics.Debug.WriteLine($"Nota: App '{APP_NAME}' talvez já exista. Status: {respApp.StatusCode}");
            }

            // 2. Tentar criar Containers (zona-descarga, zona-y, semaforo)
            string[] containers = { "zona-descarga", "zona-y", "semaforo" };
            foreach (var cont in containers)
            {
                var reqCont = new RestRequest(APP_NAME, Method.Post);
                reqCont.AddJsonBody(new { res_type = "container", resource_name = cont });

                var respCont = await client.ExecuteAsync(reqCont);

                if (!respCont.IsSuccessful)
                {
                    System.Diagnostics.Debug.WriteLine($"Nota: Container '{cont}' talvez já exista. Status: {respCont.StatusCode}");
                }
            }
        }

        // --- HELPER: ENVIAR PARA API ---
        private async Task EnviarDadosAPI(string container, string conteudo)
        {
            try
            {
                var client = new RestClient(API_BASE);
                var request = new RestRequest($"{APP_NAME}/{container}", Method.Post);

                request.AddHeader("content-type", "application/json");

                var body = new
                {
                    res_type = "content-instance",
                    resource_name = $"evt-{DateTime.Now.Ticks}",
                    content = conteudo,
                    content_type = "text/plain"
                };

                request.AddJsonBody(body);
                await client.ExecuteAsync(request);
            }
            catch (Exception ex)
            {
                // Log de erro silencioso para não parar a simulação
                System.Diagnostics.Debug.WriteLine("Erro API: " + ex.Message);
            }
        }

        // --- EVENTOS DE CLIQUE NAS VAGAS X (5 a 8) ---
        private void btn5_Click(object sender, EventArgs e) { HandleSlotClick(btn5); }
        private void btn6_Click(object sender, EventArgs e) { HandleSlotClick(btn6); }
        private void btn7_Click(object sender, EventArgs e) { HandleSlotClick(btn7); }
        private void btn8_Click(object sender, EventArgs e) { HandleSlotClick(btn8); }

        private void HandleSlotClick(Button slot)
        {
            // Só seleciona se estiver Ocupada (IndianRed)
            if (slot.BackColor == Color.IndianRed)
            {
                if (slotParaSair != null && slotParaSair != slot)
                {
                    slotParaSair.BackColor = Color.IndianRed;
                }

                // Marca como "Pronto a Sair"
                slot.BackColor = Color.Yellow;
                btnSemaforo.BackColor = Color.Yellow; // Indicação visual de operação
                slotParaSair = slot;

                // Ativa botão de saída
                btnMenos1.BackColor = Color.Yellow;
            }
            // Cancelar seleção
            else if (slot.BackColor == Color.Yellow)
            {
                slot.BackColor = Color.IndianRed;

                // Restaura estado do semáforo baseado na ocupação
                var vagasX = new[] { btn5, btn6, btn7, btn8 };
                bool full = vagasX.Count(b => b.BackColor == Color.IndianRed) >= 3; // Lógica simplificada
                btnSemaforo.BackColor = full ? Color.IndianRed : Color.LightGreen;

                slotParaSair = null;
                btnMenos1.UseVisualStyleBackColor = true;
                btnMenos1.BackColor = SystemColors.Control;
            }
        }

        // --- ENTRADA DE CAMIÕES (+1) ---
        private async void btnMais1_Click(object sender, EventArgs e)
        {
            // 1. ZONA X (5-8)
            var vagasX = new[] { btn5, btn6, btn7, btn8 };
            var primeiraLivreX = vagasX.FirstOrDefault(b => b.BackColor == Color.LightGreen);

            if (primeiraLivreX != null)
            {
                primeiraLivreX.BackColor = Color.IndianRed;

                // [API] Avisar SOMIOD que entrou em X
                await EnviarDadosAPI("zona-descarga", $"ocupado-{primeiraLivreX.Name}");

                // Atualizar Semáforo se encheu
                if (vagasX.All(b => b.BackColor == Color.IndianRed))
                {
                    btnSemaforo.BackColor = Color.IndianRed;
                    // [API] Avisar que semáforo fechou
                    await EnviarDadosAPI("semaforo", "<cmd>VERMELHO</cmd>");
                }
                return;
            }

            // 2. ZONA Y (1-4)
            var vagasY = new[] { btn1, btn2, btn3, btn4 };
            var primeiraLivreY = vagasY.FirstOrDefault(b => b.BackColor == Color.LightGreen);

            if (primeiraLivreY != null)
            {
                primeiraLivreY.BackColor = Color.IndianRed;

                // [API] Avisar SOMIOD que entrou na fila Y
                await EnviarDadosAPI("zona-y", $"ocupado-fila-{primeiraLivreY.Name}");

                if (vagasX.All(b => b.BackColor == Color.IndianRed))
                {
                    btnSemaforo.BackColor = Color.IndianRed;
                }
                return;
            }

            MessageBox.Show("Parque Completo!");
        }

        // --- SAÍDA DE CAMIÕES (-1) ---
        private async void btnMenos1_Click(object sender, EventArgs e)
        {
            if (slotParaSair == null) return;

            // 1. O camião sai da vaga X
            slotParaSair.BackColor = Color.LightGreen;

            // [API] Avisar SOMIOD que vaga X libertou
            await EnviarDadosAPI("zona-descarga", $"livre-{slotParaSair.Name}");

            btnMenos1.UseVisualStyleBackColor = true;
            btnMenos1.BackColor = SystemColors.Control;

            // 2. Verificar Fila Y
            var vagasY = new[] { btn1, btn2, btn3, btn4 };
            var camiaoEmEspera = vagasY.FirstOrDefault(b => b.BackColor == Color.IndianRed);

            if (camiaoEmEspera != null)
            {
                // [API] Semáforo VERDE (Pub)
                await EnviarDadosAPI("semaforo", "<cmd>VERDE</cmd>");

                btnSemaforo.BackColor = Color.LightGreen;
                await Task.Delay(1000); // Simulação visual

                // a) Sai de Y
                camiaoEmEspera.BackColor = Color.LightGreen;
                // [API] Saiu da fila
                await EnviarDadosAPI("zona-y", $"saiu-fila-{camiaoEmEspera.Name}");

                // b) Entra em X
                slotParaSair.BackColor = Color.IndianRed;
                // [API] Ocupou X
                await EnviarDadosAPI("zona-descarga", $"ocupado-{slotParaSair.Name}");

                // c) Atualizar Semáforo
                var vagasX = new[] { btn5, btn6, btn7, btn8 };
                if (vagasX.All(b => b.BackColor == Color.IndianRed))
                {
                    btnSemaforo.BackColor = Color.IndianRed;
                    // [API] Semáforo VERMELHO
                    await EnviarDadosAPI("semaforo", "<cmd>VERMELHO</cmd>");
                }
                else
                {
                    btnSemaforo.BackColor = Color.LightGreen;
                    await EnviarDadosAPI("semaforo", "<cmd>VERDE</cmd>");
                }

                slotParaSair = null;
                return;
            }

            // Se não havia ninguém na fila, o semáforo fica verde (livre) e limpamos seleção
            btnSemaforo.BackColor = Color.LightGreen;
            await EnviarDadosAPI("semaforo", "<cmd>VERDE</cmd>"); // Garante estado correto
            slotParaSair = null;
        }
    }
}