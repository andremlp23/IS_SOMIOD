using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks; // Necessário para o Task.Delay (animação do semáforo)
using System.Windows.Forms;

namespace IS_SOMIOD
{
    public partial class Form1 : Form
    {
        // Variável para guardar qual o botão (Vaga X) que está à espera de sair
        private Button slotParaSair = null;

        public Form1()
        {
            InitializeComponent();

            // Ligar os eventos manualmente (caso o Designer falhe)
            this.btnMais1.Click += new EventHandler(this.btnMais1_Click);
            this.btnMenos1.Click += new EventHandler(this.btnMenos1_Click);

            // Iniciar cores
            InitializeDefaultColors();
        }

        private void InitializeDefaultColors()
        {
            // Resetar todos os botões de vagas para VERDE (Livres)
            var allSlots = new[] { btn1, btn2, btn3, btn4, btn5, btn6, btn7, btn8 };
            foreach (var b in allSlots)
            {
                b.UseVisualStyleBackColor = false;
                b.BackColor = Color.LightGreen;
            }

            // Semáforo desligado (Cinza)
            btnSemaforo.BackColor = Color.LightGreen;

            // Botão de saída reset
            btnMenos1.UseVisualStyleBackColor = true;
            btnMenos1.BackColor = SystemColors.Control;

            slotParaSair = null;
        }

        // --- EVENTOS DE CLIQUE NAS VAGAS X (5 a 8) ---
        private void btn5_Click(object sender, EventArgs e) { HandleSlotClick(btn5); }
        private void btn6_Click(object sender, EventArgs e) { HandleSlotClick(btn6); }
        private void btn7_Click(object sender, EventArgs e) { HandleSlotClick(btn7); }
        private void btn8_Click(object sender, EventArgs e) { HandleSlotClick(btn8); }

        // Lógica ao clicar numa vaga X (Zona de Descarga)
        private void HandleSlotClick(Button slot)
        {
            // Só podemos sair se a vaga estiver Ocupada (Red)
            if (slot.BackColor == Color.IndianRed)
            {
                // Se já havia outro selecionado, limpa a seleção anterior
                if (slotParaSair != null && slotParaSair != slot)
                {
                    slotParaSair.BackColor = Color.IndianRed; // Volta a ocupado
                }

                // Marca este slot como "Pronto a Sair" (Amarelo)
                slot.BackColor = Color.Yellow;
                btnSemaforo.BackColor = Color.Yellow;
                slotParaSair = slot;

            }
            // Se clicar de novo num amarelo, cancela a saída
            else if (slot.BackColor == Color.Yellow)
            {
                slot.BackColor = Color.IndianRed;
                slotParaSair = null;
                btnMenos1.UseVisualStyleBackColor = true;
                btnMenos1.BackColor = SystemColors.Control;
            }
        }

        // --- ENTRADA DE CAMIÕES (+1) ---
        private void btnMais1_Click(object sender, EventArgs e)
        {
            // REQUISITO: Primeiro preencher Zona X (5-8)
            var vagasX = new[] { btn5, btn6, btn7, btn8 };
            var primeiraLivreX = vagasX.FirstOrDefault(b => b.BackColor == Color.LightGreen);

            if (primeiraLivreX != null)
            {
                primeiraLivreX.BackColor = Color.IndianRed; // Ocupa X

                // Se após ocupar todas as vagas X estiverem vermelhas, liga semáforo vermelho
                if (vagasX.All(b => b.BackColor == Color.IndianRed))
                {
                    btnSemaforo.UseVisualStyleBackColor = false;
                    btnSemaforo.BackColor = Color.IndianRed;
                }

                return;
            }

            // REQUISITO: Só depois preencher Zona Y (1-4)
            var vagasY = new[] { btn1, btn2, btn3, btn4 };
            var primeiraLivreY = vagasY.FirstOrDefault(b => b.BackColor == Color.LightGreen);

            if (primeiraLivreY != null)
            {
                primeiraLivreY.BackColor = Color.IndianRed; // Ocupa Y

                // Também verificar se, por alguma razão, todas as X estão agora vermelhas
                if (vagasX.All(b => b.BackColor == Color.IndianRed))
                {
                    btnSemaforo.UseVisualStyleBackColor = false;
                    btnSemaforo.BackColor = Color.IndianRed;
                }

                return;
            }

            // Se chegou aqui, está tudo cheio!
            MessageBox.Show("Parque Completo!");
        }

        // --- SAÍDA DE CAMIÕES (-1) ---
        private async void btnMenos1_Click(object sender, EventArgs e)
        {
            // Só funciona se tivermos selecionado uma vaga X (Amarela)
            if (slotParaSair == null) return;

            // 1. O camião sai da vaga X (Fica VERDE / Livre)
            slotParaSair.BackColor = Color.LightGreen;

            // Reset visual do botão Menos1
            btnMenos1.UseVisualStyleBackColor = true;
            btnMenos1.BackColor = SystemColors.Control;

            // 2. Verificar se há alguém na Fila de Espera (Zona Y - 1 a 4)
            // Procuramos um botão VERMELHO na zona Y
            var vagasY = new[] { btn1, btn2, btn3, btn4 };
            var camiaoEmEspera = vagasY.FirstOrDefault(b => b.BackColor == Color.IndianRed);

            if (camiaoEmEspera != null)
            {
                // REQUISITO: Semáforo VERDE para autorizar passagem
                btnSemaforo.UseVisualStyleBackColor = false;
                btnSemaforo.BackColor = Color.LightGreen;

                // Pequena pausa para simular o movimento (opcional, mas fica melhor visualmente)
                await Task.Delay(1000);

                // Lógica de Movimento:
                // a) Camião sai de Y (Fica Verde)
                camiaoEmEspera.BackColor = Color.LightGreen;

                // b) Camião entra em X (A vaga que libertámos volta a ficar Vermelha)
                slotParaSair.BackColor = Color.IndianRed;

                // c) Semáforo fecha
                var vagasX = new[] { btn5, btn6, btn7, btn8 };
                var primeiraLivreX = vagasX.FirstOrDefault(b => b.BackColor == Color.LightGreen);
                    // Se após ocupar todas as vagas X estiverem vermelhas, liga semáforo vermelho
                    if (vagasX.All(b => b.BackColor == Color.IndianRed))
                    {
                        btnSemaforo.UseVisualStyleBackColor = false;
                        btnSemaforo.BackColor = Color.IndianRed;
                    }
                else
                {
                    btnSemaforo.BackColor = Color.LightGreen;
                }

                return;
                
            }
            btnSemaforo.BackColor = Color.LightGreen;
            // Limpa a variável de seleção
            slotParaSair = null;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Vazio
        }
    }
}