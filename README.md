# ubox

Ubox es un clon de Minecraft muy simple que busca reproducir las características básicas del juego: Mundo procedural infinito y construcción por cubos. Esta simple premisa oculta un desafío significativo: el rendimiento

La solución propuesta en este proyecto consigue renderizar aproximadamente 6.291.456 cubos al mismo tiempo con un framerate alrededor de 200 FPS en una build. Conseguimos este desempeño generando una malla por cada bloque. Un bloque es una sección de 16x16x16 cubos. Para cada bloque se genera una malla en tiempo de ejecución que representa los bloques en ese segmento. 

La generación de la malla en sí misma es bastante costosa: Tiene que representar 4096 objetos en uno solo.  También lo es la generación procedural de un mundo infinito que se genera continuamente. 

Para reducir el impacto en los fotogramas por segundo, este trabajo se ejecuta en otros hilos y se evita bloquear el hilo principal del motor. Para esto se recurre a las herramientas de paralelismo de Unity: El sistema de trabajos, estructuras de datos nativas y el profiler.
